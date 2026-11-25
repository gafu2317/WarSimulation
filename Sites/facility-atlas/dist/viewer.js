const vertexSource = `#version 300 es
in vec3 position;
in vec3 normal;
in vec2 texcoord;
uniform vec3 center, axesX, axesY, axesZ, scale;
uniform vec2 frame;
out vec3 world, surfaceNormal;
out vec2 uv;
void main() {
  world = (position - center) * scale;
  surfaceNormal = normalize(normal / scale);
  uv = texcoord;
  gl_Position = vec4(dot(world, axesX) / frame.x, dot(world, axesY) / frame.y,
                     -dot(world, axesZ) / (4.0 * frame.y), 1.0);
}`;

const fragmentSource = `#version 300 es
precision highp float;
in vec3 world, surfaceNormal;
in vec2 uv;
uniform sampler2D albedo, normals;
uniform vec3 tint, shade, edge, eye;
uniform float opacity, bump, metallic, roughness, emission;
uniform int mode;
uniform bool clipAlpha;
out vec4 fragment;
void main() {
  vec4 sampleColor = texture(albedo, uv);
  if (clipAlpha && sampleColor.a < 0.42) discard;
  vec3 linearColor = pow(sampleColor.rgb, vec3(2.2));
  vec3 color = linearColor * tint;
  if (mode == 1) {
    float value = max(linearColor.r, max(linearColor.g, linearColor.b));
    color = mix(shade, tint, smoothstep(0.025, 0.24, value));
    color = mix(color, edge, smoothstep(0.24, 0.85, value));
  }
  if (mode == 2) {
    float value = dot(linearColor, vec3(0.2126, 0.7152, 0.0722));
    color = mix(tint * 0.62, min(vec3(1.0), tint * 1.52), clamp((value - 0.036) / 0.604, 0.0, 1.0));
  }
  vec3 n = normalize(surfaceNormal);
  if (!gl_FrontFacing) n = -n;
  if (bump > 0.0) {
    vec3 dx = dFdx(world), dy = dFdy(world);
    vec2 sx = dFdx(uv), sy = dFdy(uv);
    vec3 tangent = cross(dy, n) * sx.x + cross(n, dx) * sy.x;
    vec3 bitangent = cross(dy, n) * sx.y + cross(n, dx) * sy.y;
    float lengthSquared = max(dot(tangent, tangent), dot(bitangent, bitangent));
    if (lengthSquared > 1e-12) {
      vec3 map = texture(normals, uv).xyz * 2.0 - 1.0;
      n = normalize(n + (tangent * map.x + bitangent * map.y) * bump * inversesqrt(lengthSquared));
    }
  }
  vec3 light = normalize(vec3(-0.62, 1.0, -0.76));
  float sky = 0.48 + max(n.y, 0.0) * 0.23;
  float key = max(dot(n, light), 0.0) * 0.64;
  float spec = pow(max(dot(n, normalize(light + eye)), 0.0), mix(72.0, 8.0, roughness));
  vec3 lit = color * (vec3(0.92, 0.99, 1.07) * sky + vec3(1.10, 1.01, 0.87) * key + emission)
             + spec * mix(vec3(0.065), color * 0.36, metallic);
  fragment = vec4(pow(max(lit, vec3(0.0)), vec3(1.0 / 2.2)), opacity);
}`;

export const views = {
  front: { yaw: Math.PI, pitch: 0, zoom: 1 },
  iso: { yaw: Math.PI * 1.20, pitch: 0.55, zoom: 1 },
  top: { yaw: Math.PI, pitch: Math.PI / 2, zoom: 1 },
};

function shader(gl, type, source) {
  const value = gl.createShader(type);
  gl.shaderSource(value, source);
  gl.compileShader(value);
  if (!gl.getShaderParameter(value, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(value));
  return value;
}

function memo(cache, key, create) {
  if (!cache.has(key)) {
    cache.set(key, Promise.resolve().then(create).catch(error => {
      cache.delete(key);
      throw error;
    }));
  }
  return cache.get(key);
}

const geometryFiles = new Map();
const pictures = new Map();

export class Viewer {
  constructor(canvas, surfaces, preview = false) {
    const gl = canvas.getContext('webgl2', { alpha: true, antialias: true, preserveDrawingBuffer: preview });
    if (!gl) throw new Error('WebGL 2 is unavailable');
    this.canvas = canvas;
    this.gl = gl;
    this.surfaces = surfaces;
    this.preview = preview;
    this.meshes = new Map();
    this.textures = new Map();
    const program = gl.createProgram();
    gl.attachShader(program, shader(gl, gl.VERTEX_SHADER, vertexSource));
    gl.attachShader(program, shader(gl, gl.FRAGMENT_SHADER, fragmentSource));
    gl.linkProgram(program);
    if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program));
    gl.useProgram(program);
    this.attributes = Object.fromEntries(['position', 'normal', 'texcoord'].map(name => [name, gl.getAttribLocation(program, name)]));
    this.uniforms = Object.fromEntries([
      'center', 'axesX', 'axesY', 'axesZ', 'scale', 'frame', 'albedo', 'normals', 'tint', 'shade',
      'edge', 'eye', 'opacity', 'bump', 'metallic', 'roughness', 'emission', 'mode', 'clipAlpha',
    ].map(name => [name, gl.getUniformLocation(program, name)]));
    gl.uniform1i(this.uniforms.albedo, 0);
    gl.uniform1i(this.uniforms.normals, 1);
    gl.enable(gl.DEPTH_TEST);
    gl.frontFace(gl.CW);
    gl.blendFuncSeparate(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA, gl.ONE, gl.ONE_MINUS_SRC_ALPHA);
    gl.clearColor(0, 0, 0, 0);
  }

  texture(name) {
    return memo(this.textures, name, async () => {
      const image = name ? await memo(pictures, name, () => new Promise((resolve, reject) => {
        const file = new Image();
        file.onload = () => resolve(file);
        file.onerror = () => reject(new Error(`Cannot load texture ${name}`));
        file.src = `models/${name}`;
      })) : null;
      const gl = this.gl;
      const texture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      if (image) {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, image);
      } else {
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([255, 255, 255, 255]));
      }
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR_MIPMAP_LINEAR);
      const anisotropy = gl.getExtension('EXT_texture_filter_anisotropic');
      if (anisotropy) gl.texParameterf(gl.TEXTURE_2D, anisotropy.TEXTURE_MAX_ANISOTROPY_EXT, 4);
      gl.generateMipmap(gl.TEXTURE_2D);
      return texture;
    });
  }

  load(model) {
    return memo(this.meshes, model.id, async () => {
      const geometry = memo(geometryFiles, model.id, async () => {
        const response = await fetch(`models/${model.id}.bin`);
        if (!response.ok) throw new Error(`Cannot load model ${model.id}`);
        return response.arrayBuffer();
      });
      const surfaces = model.materials.map(index => this.surfaces[index]);
      const [raw, materials] = await Promise.all([geometry, Promise.all(surfaces.map(async surface => ({
        ...surface,
        albedo: await this.texture(surface.base),
        normals: await this.texture(this.preview ? '' : surface.normal),
      })))]);
      const gl = this.gl;
      const layout = gl.createVertexArray();
      gl.bindVertexArray(layout);
      const vertices = gl.createBuffer();
      gl.bindBuffer(gl.ARRAY_BUFFER, vertices);
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(raw, 0, model.vertices * 8), gl.STATIC_DRAW);
      for (const [name, size, offset] of [['position', 3, 0], ['normal', 3, 12], ['texcoord', 2, 24]]) {
        const location = this.attributes[name];
        gl.enableVertexAttribArray(location);
        gl.vertexAttribPointer(location, size, gl.FLOAT, false, 32, offset);
      }
      const indices = gl.createBuffer();
      gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indices);
      gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint32Array(raw, model.vertices * 32), gl.STATIC_DRAW);
      gl.bindVertexArray(null);
      let offset = 0;
      const parts = materials.map((material, index) => {
        const count = model.indices[index];
        const part = { material, offset, count };
        offset += 4 * count;
        return part;
      });
      return {
        model, layout,
        opaque: parts.filter(part => part.material.opacity >= 0.99),
        glass: parts.filter(part => part.material.opacity < 0.99),
      };
    });
  }

  draw(mesh, view) {
    const gl = this.gl;
    const u = this.uniforms;
    const { width, height } = this.canvas;
    gl.viewport(0, 0, width, height);
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
    if (!mesh) return;
    const { low, high, scale } = mesh.model;
    const center = low.map((value, axis) => (value + high[axis]) / 2);
    const extent = low.map((value, axis) => (high[axis] - value) * scale[axis] / 2);
    const radius = Math.hypot(...extent);
    const yaw = view.yaw, pitch = view.pitch;
    const x = [-Math.cos(yaw), 0, Math.sin(yaw)];
    const y = [-Math.sin(yaw) * Math.sin(pitch), Math.cos(pitch), -Math.cos(yaw) * Math.sin(pitch)];
    const z = [Math.sin(yaw) * Math.cos(pitch), Math.sin(pitch), Math.cos(yaw) * Math.cos(pitch)];
    const aspect = width / height;
    const span = Math.max(radius * (this.preview ? 0.86 : 0.95), radius * 0.93 / aspect) / view.zoom;
    gl.uniform3fv(u.center, center);
    gl.uniform3fv(u.scale, scale);
    gl.uniform3fv(u.axesX, x);
    gl.uniform3fv(u.axesY, y);
    gl.uniform3fv(u.axesZ, z);
    gl.uniform3fv(u.eye, z);
    gl.uniform2f(u.frame, Math.max(0.01, span * aspect), span);
    gl.bindVertexArray(mesh.layout);
    this.drawParts(mesh.opaque);
    gl.enable(gl.BLEND);
    gl.depthMask(false);
    this.drawParts(mesh.glass);
    gl.depthMask(true);
    gl.disable(gl.BLEND);
    gl.bindVertexArray(null);
  }

  drawParts(parts) {
    const gl = this.gl, u = this.uniforms;
    for (const { material, count, offset } of parts) {
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, material.albedo);
      gl.activeTexture(gl.TEXTURE1);
      gl.bindTexture(gl.TEXTURE_2D, material.normals);
      gl.uniform3fv(u.tint, material.color.slice(0, 3));
      gl.uniform3fv(u.shade, (material.roofShade ?? material.color).slice(0, 3));
      gl.uniform3fv(u.edge, (material.roofEdge ?? material.color).slice(0, 3));
      gl.uniform1f(u.opacity, material.opacity);
      gl.uniform1f(u.bump, material.normal && !this.preview ? material.bump : 0);
      gl.uniform1f(u.metallic, material.metallic);
      gl.uniform1f(u.roughness, material.roughness);
      gl.uniform1f(u.emission, material.emission);
      gl.uniform1i(u.mode, material.roof ? 1 : material.tone ? 2 : 0);
      gl.uniform1i(u.clipAlpha, material.cutout);
      gl.drawElements(gl.TRIANGLES, count, gl.UNSIGNED_INT, offset);
    }
  }
}
