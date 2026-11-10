using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Sprites;
using UnityEngine.Serialization;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class CountryFacilitySymbol : MaskableGraphic
{
    [SerializeField] private string _facility;
    [FormerlySerializedAs("_schoolIcons"), SerializeField] private Sprite[] _icons;
    public Sprite Icon
    {
        get
        {
            int index = _facility switch {
                "戦士学校" => 0, "魔法学校" => 1, "教会" => 2, "精霊使役学校" => 3, "鍛冶場" => 4,
                "図書館" => 5, "広場" => 6, "酒場" => 7, "天文台" => 8, "闘技場" => 9,
                "美術館" => 10, "診療所" => 11, "カジノ" => 12, "衛兵所" => 13, "歓楽街" => 14, _ => -1
            };
            return _icons != null && index >= 0 && index < _icons.Length ? _icons[index] : null;
        }
    }
    public bool HasImage => Icon != null;
    public override Texture mainTexture => Icon != null ? Icon.texture : base.mainTexture;

    public void SetFacility(string facility) { _facility = facility; color = HasImage ? Color.white : CountryGovernmentTheme.Accent; SetAllDirty(); }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        var sprite = Icon;
        if (sprite != null)
        {
            var rect = GetPixelAdjustedRect();
            var size = sprite.rect.size * Mathf.Min(rect.width / sprite.rect.width, rect.height / sprite.rect.height);
            var min = rect.center - size * .5f; var max = min + size;
            var uv = DataUtility.GetOuterUV(sprite);
            mesh.AddVert(new Vector2(min.x, min.y), color, new Vector2(uv.x, uv.y));
            mesh.AddVert(new Vector2(min.x, max.y), color, new Vector2(uv.x, uv.w));
            mesh.AddVert(new Vector2(max.x, max.y), color, new Vector2(uv.z, uv.w));
            mesh.AddVert(new Vector2(max.x, min.y), color, new Vector2(uv.z, uv.y));
            mesh.AddTriangle(0, 1, 2); mesh.AddTriangle(2, 3, 0);
            return;
        }
        switch (_facility)
        {
            case "選択":
                Line(mesh, new(22, 50), new(42, 30), 9); Line(mesh, new(42, 30), new(79, 74), 9); break;
            case "戦士学校":
                Polygon(mesh, new(46, 38), new(46, 77), new(50, 88), new(54, 77), new(54, 38));
                Line(mesh, new(34, 38), new(66, 38), 6); Line(mesh, new(50, 18), new(50, 38), 7);
                Line(mesh, new(44, 17), new(56, 17), 6); break;
            case "魔法学校":
                Line(mesh, new(28, 20), new(61, 64), 6);
                Star(mesh, new(66, 74), 17); Star(mesh, new(30, 72), 6); break;
            case "教会":
                Line(mesh, new(50, 18), new(50, 84), 10); Line(mesh, new(25, 59), new(75, 59), 10); break;
            case "精霊使役学校":
                Polygon(mesh, new(50, 20), new(31, 49), new(50, 84), new(69, 49));
                Line(mesh, new(18, 37), new(18, 61), 3); Line(mesh, new(82, 37), new(82, 61), 3);
                Star(mesh, new(18, 72), 5); Star(mesh, new(82, 26), 5); break;
            case "鍛冶場":
                Polygon(mesh, new(16, 64), new(83, 64), new(74, 49), new(24, 49));
                Line(mesh, new(50, 28), new(50, 49), 20);
                Polygon(mesh, new(30, 24), new(70, 24), new(60, 30), new(40, 30));
                Line(mesh, new(49, 76), new(76, 76), 9); break;
            case "図書館":
                Outline(mesh, new(50, 21), new(18, 31), new(18, 78), new(50, 68), new(82, 78), new(82, 31), new(50, 21));
                Line(mesh, new(50, 21), new(50, 68), 4);
                Line(mesh, new(27, 61), new(40, 57), 3); Line(mesh, new(60, 57), new(73, 61), 3); break;
            case "天文台":
                Line(mesh, new(25, 53), new(74, 76), 16); Line(mesh, new(76, 65), new(84, 83), 5);
                Line(mesh, new(48, 51), new(48, 38), 5); Line(mesh, new(48, 38), new(28, 16), 4); Line(mesh, new(48, 38), new(68, 16), 4);
                Star(mesh, new(22, 81), 6); break;
            case "工期":
                for (int i = 0; i < 32; i++)
                {
                    float a = i * Mathf.PI / 16, b = (i + 1) * Mathf.PI / 16;
                    Line(mesh, new Vector2(50, 50) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 31, new Vector2(50, 50) + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * 31, 4);
                }
                Line(mesh, new(50, 50), new(50, 70), 4); Line(mesh, new(50, 50), new(66, 42), 4); break;
            default:
                Outline(mesh, new(21, 20), new(21, 63), new(50, 83), new(79, 63), new(79, 20), new(21, 20));
                Line(mesh, new(43, 21), new(43, 44), 5); Line(mesh, new(57, 21), new(57, 44), 5); break;
        }
    }

    private void Star(VertexHelper mesh, Vector2 center, float size)
    {
        for (int i = 0; i < 4; i++)
        {
            float angle = i * Mathf.PI * .5f;
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var side = new Vector2(-direction.y, direction.x) * size * .3f;
            Polygon(mesh, center, center + direction * size * .3f + side, center + direction * size, center + direction * size * .3f - side);
        }
    }

    private void Outline(VertexHelper mesh, params Vector2[] points)
    {
        for (int i = 1; i < points.Length; i++) Line(mesh, points[i - 1], points[i], 4);
    }

    private void Line(VertexHelper mesh, Vector2 from, Vector2 to, float width)
    {
        var normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
        Polygon(mesh, from - normal, from + normal, to + normal, to - normal);
    }

    private void Polygon(VertexHelper mesh, params Vector2[] points)
    {
        int start = mesh.currentVertCount;
        var rect = GetPixelAdjustedRect();
        foreach (var point in points)
            mesh.AddVert(new Vector2(rect.xMin + point.x * rect.width / 100, rect.yMin + point.y * rect.height / 100), color, Vector2.zero);
        for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
    }
}
