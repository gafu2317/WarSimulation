import { Viewer, views } from './viewer.js';

const $ = selector => document.querySelector(selector);
const canvas = $('#viewer');
const list = $('#results');
const status = $('#status');
const search = $('#search');
const previewCanvas = document.createElement('canvas');
previewCanvas.width = 156;
previewCanvas.height = 122;

let catalog, viewer, preview, current, mesh;
let view = { ...views.iso };
let filter = 'すべて';
let requestId = 0;
let animationFrame = 0;
let observer;
const thumbs = new Map();
const pointers = new Map();

function scheduleDraw() {
  if (animationFrame || !viewer) return;
  animationFrame = requestAnimationFrame(() => {
    animationFrame = 0;
    const density = Math.min(2, window.devicePixelRatio || 1);
    const width = Math.max(1, Math.round(canvas.clientWidth * density));
    const height = Math.max(1, Math.round(canvas.clientHeight * density));
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }
    viewer.draw(mesh, view);
  });
}

function setView(key) {
  if (key) view = { ...views[key] };
  for (const button of document.querySelectorAll('[data-view]')) {
    button.setAttribute('aria-pressed', String(button.dataset.view === key));
  }
  scheduleDraw();
}

function changeZoom(amount) {
  view.zoom = Math.min(3.5, Math.max(0.6, view.zoom * amount));
  scheduleDraw();
}

function changeAngles(dx, dy) {
  view.yaw += dx;
  view.pitch = Math.min(Math.PI / 2, Math.max(-1.15, view.pitch + dy));
  setView();
}

async function select(model) {
  const id = ++requestId;
  current = model;
  mesh = null;
  setView('iso');
  $('#building-name').textContent = model.name;
  $('#building-group').textContent = model.group;
  const level = model.id.match(/_Lv(\d+)$/)?.[1];
  $('#building-level').textContent = level ? `Lv.${level}` : '';
  $('#building-description').textContent = model.description;
  const dimensions = [0, 2, 1].map(axis => ((model.high[axis] - model.low[axis]) * model.scale[axis]).toFixed(1));
  $('#building-size').textContent = dimensions.join(' × ') + ' m';
  for (const button of list.querySelectorAll('[data-model]')) {
    button.setAttribute('aria-pressed', String(button.dataset.model === model.id));
  }
  if (!viewer) return;
  status.textContent = 'モデルを読み込み中';
  try {
    const loaded = await viewer.load(model);
    if (id !== requestId) return;
    mesh = loaded;
    status.textContent = '';
    scheduleDraw();
  } catch (error) {
    if (id !== requestId) return;
    status.textContent = 'モデルを読み込めなかった';
    console.error(error);
  }
}

function thumbnail(model) {
  if (!thumbs.has(model.id)) {
    const promise = preview.load(model).then(value => {
      preview.draw(value, views.iso);
      return previewCanvas.toDataURL('image/png');
    });
    thumbs.set(model.id, promise);
  }
  return thumbs.get(model.id);
}

function renderList() {
  const query = search.value.normalize('NFKC').trim().toLowerCase();
  const matches = catalog.models.filter(model =>
    (filter === 'すべて' || filter === model.group)
      && [model.name, model.id, model.prototype, model.description].some(value => value.toLowerCase().includes(query)),
  );
  $('#count').textContent = `${matches.length} 件`;
  observer?.disconnect();
  list.replaceChildren();
  for (const model of matches) {
    const button = document.createElement('button');
    button.className = 'facility';
    button.dataset.model = model.id;
    button.type = 'button';
    button.setAttribute('aria-pressed', String(model === current));
    button.innerHTML = '<span class="thumb" aria-hidden="true"></span><span class="row-label"><b></b><small></small></span><span class="indicator" aria-hidden="true"></span>';
    button.querySelector('b').textContent = model.name;
    button.querySelector('small').textContent = model.group;
    button.onclick = () => {
      select(model);
      if (window.matchMedia('(max-width: 760px)').matches) {
        $('#stage').scrollIntoView({ behavior: matchMedia('(prefers-reduced-motion: reduce)').matches ? 'instant' : 'smooth' });
      }
    };
    list.append(button);
    observer?.observe(button);
  }
  if (!matches.length) {
    const empty = document.createElement('p');
    empty.className = 'empty';
    empty.textContent = '一致する施設はない';
    list.append(empty);
  }
  list.scrollTop = 0;
}

function trackPointer(event) {
  if (!pointers.has(event.pointerId)) return;
  const before = pointers.get(event.pointerId);
  const after = [event.clientX, event.clientY];
  if (pointers.size === 2) {
    const other = [...pointers.entries()].find(([id]) => id !== event.pointerId)[1];
    const distance = point => Math.hypot(point[0] - other[0], point[1] - other[1]);
    changeZoom(Math.max(1, distance(after)) / Math.max(1, distance(before)));
  } else if (pointers.size === 1) {
    changeAngles((after[0] - before[0]) * 0.009, (after[1] - before[1]) * 0.009);
  }
  pointers.set(event.pointerId, after);
}

function connectControls() {
  new ResizeObserver(scheduleDraw).observe(canvas);
  canvas.addEventListener('pointerdown', event => {
    if (event.pointerType === 'mouse' && event.button !== 0) return;
    pointers.set(event.pointerId, [event.clientX, event.clientY]);
    canvas.setPointerCapture(event.pointerId);
  });
  canvas.addEventListener('pointermove', trackPointer);
  canvas.addEventListener('lostpointercapture', event => pointers.delete(event.pointerId));
  canvas.addEventListener('wheel', event => {
    event.preventDefault();
    changeZoom(Math.exp(-Math.max(-150, Math.min(150, event.deltaY)) * 0.002));
  }, { passive: false });
  const directions = { ArrowLeft: [-0.14, 0], ArrowRight: [0.14, 0], ArrowUp: [0, 0.14], ArrowDown: [0, -0.14] };
  canvas.addEventListener('keydown', event => {
    if (directions[event.key]) changeAngles(...directions[event.key]);
    else if (event.key === '+' || event.key === '=') changeZoom(1.2);
    else if (event.key === '-') changeZoom(1 / 1.2);
    else return;
    event.preventDefault();
  });
  for (const button of document.querySelectorAll('[data-view]')) {
    button.onclick = () => setView(button.dataset.view);
  }
  for (const button of document.querySelectorAll('[data-zoom]')) {
    button.onclick = () => changeZoom(1.2 ** Number(button.dataset.zoom));
  }
}

try {
  const response = await fetch('catalog.json');
  if (!response.ok) throw new Error('Cannot load catalog');
  catalog = await response.json();
  $('#total').textContent = catalog.models.length;
  try {
    viewer = new Viewer(canvas, catalog.surfaces);
    preview = new Viewer(previewCanvas, catalog.surfaces, true);
    connectControls();
    observer = new IntersectionObserver(entries => {
      for (const { target, isIntersecting } of entries) {
        if (!isIntersecting) continue;
        observer.unobserve(target);
        const model = catalog.models.find(item => item.id === target.dataset.model);
        thumbnail(model).then(url => {
          const image = new Image();
          image.alt = '';
          image.src = url;
          target.querySelector('.thumb').append(image);
        }).catch(console.error);
      }
    }, { root: list, rootMargin: '70px' });
  } catch (error) {
    status.textContent = 'このブラウザーでは3Dビューを表示できない';
    console.error(error);
  }
  const filters = $('#filters');
  for (const group of ['すべて', ...new Set(catalog.models.map(model => model.group))]) {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = group;
    button.setAttribute('aria-pressed', String(group === filter));
    button.onclick = () => {
      filter = group;
      for (const chip of filters.children) chip.setAttribute('aria-pressed', String(chip === button));
      renderList();
    };
    filters.append(button);
  }
  select(catalog.models[0]);
  renderList();
  search.addEventListener('input', renderList);
} catch (error) {
  status.textContent = '施設一覧を読み込めなかった';
  console.error(error);
}
