using UnityEngine;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryGovernmentPlate : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Min(0)] private float _cut = 6;
    [SerializeField, Min(0)] private float _stroke;
    [SerializeField, Range(0, .15f)] private float _shade = .035f;
    [SerializeField] private Texture2D _texture;
    [SerializeField, Min(1)] private float _tileSize = 256;
    private Vector3 _snapPosition, _snapScale;
    private Quaternion _snapRotation;
    private bool _watching, _snapReady;
    public override Texture mainTexture => _texture != null ? _texture : base.mainTexture;

    public void SetTexture(Texture2D texture, float tileSize = 256)
    {
        _texture = texture; _tileSize = Mathf.Max(1, tileSize);
        SetAllDirty();
    }

    public void Configure(float cut = 6, float stroke = 0, float shade = .035f)
    {
        _cut = cut; _stroke = stroke; _shade = shade;
        WatchBorder(); SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable(); WatchBorder();
    }

    protected override void OnDisable()
    {
        if (_watching) Canvas.preWillRenderCanvases -= UpdateBorder;
        _watching = _snapReady = false; base.OnDisable();
    }

    private void WatchBorder()
    {
        bool watch = isActiveAndEnabled && _stroke > 0;
        if (watch == _watching) return;
        _watching = watch; _snapReady = false;
        if (watch) Canvas.preWillRenderCanvases += UpdateBorder;
        else Canvas.preWillRenderCanvases -= UpdateBorder;
    }

    private void UpdateBorder()
    {
        var position = transform.position; var scale = transform.lossyScale; var rotation = transform.rotation;
        if (_snapReady && position == _snapPosition && scale == _snapScale && rotation == _snapRotation) return;
        _snapPosition = position; _snapScale = scale; _snapRotation = rotation; _snapReady = true;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        float insetX = _stroke, insetY = _stroke;
        if (_stroke > 0) SnapBorder(ref rect, ref insetX, ref insetY);
        AddContour(mesh, rect, _cut);
        if (_stroke <= 0)
        {
            for (int i = 1; i < 7; i++) mesh.AddTriangle(0, i, i + 1);
            return;
        }
        insetX = Mathf.Min(insetX, rect.width * .5f);
        insetY = Mathf.Min(insetY, rect.height * .5f);
        AddContour(mesh, Rect.MinMaxRect(rect.xMin + insetX, rect.yMin + insetY, rect.xMax - insetX, rect.yMax - insetY), Mathf.Max(0, _cut - Mathf.Min(insetX, insetY) * .6f));
        for (int i = 0; i < 8; i++)
        {
            int next = (i + 1) % 8;
            mesh.AddTriangle(i, next, i + 8);
            mesh.AddTriangle(next, next + 8, i + 8);
        }
    }

    private void SnapBorder(ref Rect rect, ref float insetX, ref float insetY)
    {
        if (canvas == null || canvas.rootCanvas.renderMode != UnityEngine.RenderMode.ScreenSpaceOverlay) return;
        var right = rectTransform.TransformVector(Vector3.right);
        var up = rectTransform.TransformVector(Vector3.up);
        if (right.x <= 0 || up.y <= 0 || Mathf.Abs(right.y) > .001f || Mathf.Abs(up.x) > .001f) return;
        if (_stroke * right.x < 1.5f && _stroke * up.y < 1.5f) return;
        var min = rectTransform.TransformPoint(rect.min);
        var max = rectTransform.TransformPoint(rect.max);
        rect = Rect.MinMaxRect(rect.xMin + (Mathf.Round(min.x) - min.x) / right.x,
            rect.yMin + (Mathf.Round(min.y) - min.y) / up.y,
            rect.xMax + (Mathf.Round(max.x) - max.x) / right.x,
            rect.yMax + (Mathf.Round(max.y) - max.y) / up.y);
        insetX = Mathf.Max(1, Mathf.Round(_stroke * right.x)) / right.x;
        insetY = Mathf.Max(1, Mathf.Round(_stroke * up.y)) / up.y;
    }

    private void AddContour(UnityEngine.UI.VertexHelper mesh, Rect rect, float cut)
    {
        cut = Mathf.Min(cut, Mathf.Min(rect.width, rect.height) * .25f);
        Vertex(mesh, new Vector2(rect.xMin + cut, rect.yMin), rect);
        Vertex(mesh, new Vector2(rect.xMax - cut, rect.yMin), rect);
        Vertex(mesh, new Vector2(rect.xMax, rect.yMin + cut), rect);
        Vertex(mesh, new Vector2(rect.xMax, rect.yMax - cut), rect);
        Vertex(mesh, new Vector2(rect.xMax - cut, rect.yMax), rect);
        Vertex(mesh, new Vector2(rect.xMin + cut, rect.yMax), rect);
        Vertex(mesh, new Vector2(rect.xMin, rect.yMax - cut), rect);
        Vertex(mesh, new Vector2(rect.xMin, rect.yMin + cut), rect);
    }

    private void Vertex(UnityEngine.UI.VertexHelper mesh, Vector2 point, Rect rect)
    {
        var uv = new Vector2(Mathf.InverseLerp(rect.xMin, rect.xMax, point.x), Mathf.InverseLerp(rect.yMin, rect.yMax, point.y));
        var tint = color * Mathf.Lerp(1 - _shade, 1 + _shade, uv.y); tint.a = color.a;
        var textureUV = _texture != null ? (point - rect.min) / _tileSize : uv;
        mesh.AddVert(point, tint, textureUV);
    }
}
