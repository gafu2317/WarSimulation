using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryReportPaper : MaskableGraphic
{
    [SerializeField] private Texture2D _texture;
    [SerializeField] private float _fold = 48;
    public override Texture mainTexture => _texture != null ? _texture : base.mainTexture;
    public void Configure(Texture2D texture, float fold) { _texture = texture; _fold = fold; SetAllDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var rect = GetPixelAdjustedRect();
        if (rect.width <= 0 || rect.height <= 0) return;
        float fold = Mathf.Min(_fold, Mathf.Min(rect.width, rect.height) * .25f);
        Add(new Vector2(rect.xMin, rect.yMin));
        Add(new Vector2(rect.xMax, rect.yMin));
        Add(new Vector2(rect.xMax, rect.yMax - fold));
        Add(new Vector2(rect.xMax - fold, rect.yMax));
        Add(new Vector2(rect.xMin, rect.yMax));
        for (int i = 1; i < 4; i++) mesh.AddTriangle(0, i, i + 1);
        void Add(Vector2 p) => mesh.AddVert(p, color, new Vector2((p.x - rect.xMin) / rect.width, (p.y - rect.yMin) / rect.height));
    }
}
