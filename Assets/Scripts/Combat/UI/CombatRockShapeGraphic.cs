using UnityEngine;
using UnityEngine.UI;

public enum CombatRockShape { Straight, Bent, U, Circle }

public readonly struct CombatRockPlacement
{
    public CombatRockShape Shape { get; }
    public Vector2 Position { get; }
    public int QuarterTurns { get; }

    public CombatRockPlacement(CombatRockShape shape, Vector2 position, int quarterTurns)
    {
        Shape = shape;
        Position = position;
        QuarterTurns = shape == CombatRockShape.Circle ? 0 : (quarterTurns % 4 + 4) % 4;
    }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CombatRockShapeGraphic : MaskableGraphic
{
    private CombatRockShape _shape;
    public bool FilledCircle { get; set; }
    public CombatRockShape Shape
    {
        get => _shape;
        set { _shape = value; SetVerticesDirty(); }
    }

    public static string Label(CombatRockShape shape) => shape switch
    {
        CombatRockShape.Straight => "Iの字",
        CombatRockShape.Bent => "くの字",
        CombatRockShape.U => "Uの字",
        _ => "まる型",
    };

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float scale = Mathf.Min(rect.width, rect.height) * 0.36f;
        Vector2 center = rect.center;
        switch (_shape)
        {
            case CombatRockShape.Straight:
                Segment(vh, center + Vector2.down * scale, center + Vector2.up * scale, scale * (2f / 3f));
                break;
            case CombatRockShape.Bent:
                Segment(vh, center + new Vector2(0.6f, 1f) * scale, center + Vector2.left * scale, scale * (2f / 3f));
                Segment(vh, center + Vector2.left * scale, center + new Vector2(0.6f, -1f) * scale, scale * (2f / 3f));
                break;
            case CombatRockShape.U:
                Segment(vh, center + new Vector2(-1, 1) * scale, center + new Vector2(-1, -1) * scale, scale * (2f / 3f));
                Segment(vh, center + new Vector2(-1, -1) * scale, center + new Vector2(1, -1) * scale, scale * (2f / 3f));
                Segment(vh, center + new Vector2(1, -1) * scale, center + new Vector2(1, 1) * scale, scale * (2f / 3f));
                break;
            default:
                if (FilledCircle)
                {
                    vh.AddVert(center, color, Vector2.zero);
                    for (int i = 0; i <= 32; i++)
                    {
                        float angle = i * Mathf.PI / 16f;
                        vh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * scale, color, Vector2.zero);
                        if (i > 0) vh.AddTriangle(0, i + 1, i);
                    }
                    break;
                }
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16f;
                    float b = (i + 1) * Mathf.PI / 16f;
                    Segment(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * scale,
                        center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * scale, scale * (2f / 3f), extendEnds: false);
                }
                break;
        }
    }

    private void Segment(VertexHelper vh, Vector2 a, Vector2 b, float width, bool extendEnds = true)
    {
        Vector2 delta = (b - a).normalized;
        if (extendEnds)
        {
            a -= delta * width * 0.5f;
            b += delta * width * 0.5f;
        }
        Vector2 normal = new Vector2(-delta.y, delta.x) * width * 0.5f;
        int start = vh.currentVertCount;
        vh.AddVert(a - normal, color, Vector2.zero);
        vh.AddVert(a + normal, color, Vector2.zero);
        vh.AddVert(b + normal, color, Vector2.zero);
        vh.AddVert(b - normal, color, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
