using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class CombatCharacterAppearanceView : MonoBehaviour
{
    public enum Facing
    {
        FrontLeft = 0,
        FrontRight = 1,
        BackLeft = 2,
        BackRight = 3,
    }

    private const string SpriteRootName = "SpriteRoot";
    private const string FrontLeftRootName = "CharacterFrontLeft";
    private const string FrontRightRootName = "CharacterFrontRight";
    private const string BackLeftRootName = "CharacterBackLeft";
    private const string BackRightRootName = "CharacterBackRight";

    [SerializeField, Min(16f)] private float _previewWidth = 108f;
    [SerializeField, Min(16f)] private float _previewHeight = 108f;

    private readonly List<Image> _partImages = new();
    private readonly List<PartSnapshot> _snapshots = new();
    private RectTransform _rectTransform;
    private RectTransform _contentRoot;
    private bool _usesPrefabContent;

    public int PartCount => _partImages.Count;

    private void Awake()
    {
        EnsureBuilt();
    }

    public void Bind(Character character, Facing facing)
    {
        EnsureBuilt();
        ClearParts();

        if (character == null)
        {
            return;
        }

        if (_usesPrefabContent)
        {
            return;
        }

        if (!TryBuildSnapshots(character.transform, facing, _snapshots, out Bounds bounds))
        {
            return;
        }

        Vector3 center = bounds.center;
        float height = Mathf.Max(0.001f, bounds.size.y);
        float width = Mathf.Max(0.001f, bounds.size.x);
        float scale = Mathf.Min(_previewWidth / width, _previewHeight / height);

        _contentRoot.sizeDelta = new Vector2(width * scale, height * scale);

        for (int i = 0; i < _snapshots.Count; i++)
        {
            CreatePart(_snapshots[i], center, scale, i);
        }
    }

    private void EnsureBuilt()
    {
        if (_rectTransform != null && _contentRoot != null)
        {
            return;
        }

        _rectTransform = GetComponent<RectTransform>();

        Transform mask = transform.Find("Mask");
        Transform prefabContent = mask != null ? mask.Find("Content") : null;
        if (prefabContent != null && prefabContent.childCount > 0)
        {
            _contentRoot = prefabContent as RectTransform;
            _usesPrefabContent = _contentRoot != null;
            if (_usesPrefabContent)
            {
                return;
            }
        }

        _usesPrefabContent = false;
        Transform contentParent = mask != null ? mask : transform;
        var contentObject = new GameObject("RuntimeContent", typeof(RectTransform));
        contentObject.transform.SetParent(contentParent, false);
        _contentRoot = contentObject.GetComponent<RectTransform>();
        _contentRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _contentRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _contentRoot.pivot = new Vector2(0.5f, 0.5f);
        _contentRoot.anchoredPosition = Vector2.zero;
        _contentRoot.sizeDelta = new Vector2(_previewWidth, _previewHeight);
    }

    private void ClearParts()
    {
        for (int i = 0; i < _partImages.Count; i++)
        {
            if (_partImages[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(_partImages[i].gameObject);
            }
            else
            {
                DestroyImmediate(_partImages[i].gameObject);
            }
        }

        _partImages.Clear();
        _snapshots.Clear();
    }

    private void CreatePart(PartSnapshot snapshot, Vector3 center, float scale, int siblingIndex)
    {
        var imageObject = new GameObject(snapshot.Name, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(_contentRoot, false);
        imageObject.transform.SetSiblingIndex(siblingIndex);

        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = snapshot.Pivot;
        rect.sizeDelta = new Vector2(snapshot.Size.x * scale, snapshot.Size.y * scale);

        Vector3 relativePosition = snapshot.LocalPosition - center;
        rect.anchoredPosition = new Vector2(relativePosition.x * scale, relativePosition.y * scale);
        rect.localRotation = snapshot.LocalRotation;
        rect.localScale = Vector3.one;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = snapshot.Sprite;
        image.color = snapshot.Color;
        image.preserveAspect = true;
        image.raycastTarget = false;

        _partImages.Add(image);
    }

    private static bool TryBuildSnapshots(
        Transform characterRoot,
        Facing facing,
        List<PartSnapshot> snapshots,
        out Bounds bounds)
    {
        bounds = default;
        Transform directionRoot = FindDirectionRoot(characterRoot, facing);
        if (directionRoot == null)
        {
            return false;
        }

        SpriteRenderer[] renderers = directionRoot.GetComponentsInChildren<SpriteRenderer>(true);
        var ordered = new List<SpriteRenderer>(renderers.Length);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || !renderer.enabled)
            {
                continue;
            }

            if (!IsVisibleWithinDirectionRoot(renderer.transform, directionRoot))
            {
                continue;
            }

            ordered.Add(renderer);
        }

        if (ordered.Count == 0)
        {
            return false;
        }

        ordered.Sort(CompareRenderers);

        bool hasBounds = false;
        Matrix4x4 rootWorldToLocal = directionRoot.worldToLocalMatrix;
        for (int i = 0; i < ordered.Count; i++)
        {
            SpriteRenderer renderer = ordered[i];
            PartSnapshot snapshot = BuildSnapshot(renderer, directionRoot, rootWorldToLocal);
            snapshots.Add(snapshot);

            if (!hasBounds)
            {
                bounds = snapshot.Bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(snapshot.Bounds.min);
                bounds.Encapsulate(snapshot.Bounds.max);
            }
        }

        return hasBounds;
    }

    private static PartSnapshot BuildSnapshot(SpriteRenderer renderer, Transform directionRoot, Matrix4x4 rootWorldToLocal)
    {
        Sprite sprite = renderer.sprite;
        Matrix4x4 localMatrix = rootWorldToLocal * renderer.transform.localToWorldMatrix;
        Bounds spriteBounds = sprite.bounds;
        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, 0f);
        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, 0f);

        Vector3[] corners =
        {
            new Vector3(spriteBounds.min.x, spriteBounds.min.y, 0f),
            new Vector3(spriteBounds.min.x, spriteBounds.max.y, 0f),
            new Vector3(spriteBounds.max.x, spriteBounds.min.y, 0f),
            new Vector3(spriteBounds.max.x, spriteBounds.max.y, 0f),
        };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 corner = localMatrix.MultiplyPoint3x4(corners[i]);
            min = Vector3.Min(min, corner);
            max = Vector3.Max(max, corner);
        }

        Vector3 localPosition = localMatrix.MultiplyPoint3x4(sprite.bounds.center);
        Vector3 relativeScale = GetRelativeScale(directionRoot, renderer.transform);
        Quaternion relativeRotation = Quaternion.Inverse(directionRoot.rotation) * renderer.transform.rotation;

        return new PartSnapshot(
            renderer.gameObject.name,
            sprite,
            renderer.color,
            localPosition,
            relativeRotation,
            new Vector2(
                sprite.rect.width / sprite.pixelsPerUnit * Mathf.Abs(relativeScale.x),
                sprite.rect.height / sprite.pixelsPerUnit * Mathf.Abs(relativeScale.y)),
            new Vector2(
                sprite.pivot.x / sprite.rect.width,
                sprite.pivot.y / sprite.rect.height),
            new Bounds((min + max) * 0.5f, max - min));
    }

    private static Vector3 GetRelativeScale(Transform root, Transform target)
    {
        Vector3 rootScale = root.lossyScale;
        Vector3 targetScale = target.lossyScale;
        return new Vector3(
            Mathf.Approximately(rootScale.x, 0f) ? 1f : targetScale.x / rootScale.x,
            Mathf.Approximately(rootScale.y, 0f) ? 1f : targetScale.y / rootScale.y,
            Mathf.Approximately(rootScale.z, 0f) ? 1f : targetScale.z / rootScale.z);
    }

    private static bool IsVisibleWithinDirectionRoot(Transform target, Transform directionRoot)
    {
        Transform current = target;
        while (current != null && current != directionRoot)
        {
            if (!current.gameObject.activeSelf)
            {
                return false;
            }

            current = current.parent;
        }

        return current == directionRoot;
    }

    private static int CompareRenderers(SpriteRenderer x, SpriteRenderer y)
    {
        int sortingLayer = x.sortingLayerID.CompareTo(y.sortingLayerID);
        if (sortingLayer != 0)
        {
            return sortingLayer;
        }

        int sortingOrder = x.sortingOrder.CompareTo(y.sortingOrder);
        if (sortingOrder != 0)
        {
            return sortingOrder;
        }

        return string.CompareOrdinal(x.gameObject.name, y.gameObject.name);
    }

    private static Transform FindDirectionRoot(Transform characterRoot, Facing facing)
    {
        Transform spriteRoot = FindNamedTransform(characterRoot, SpriteRootName);
        if (spriteRoot == null)
        {
            return null;
        }

        string rootName = facing switch
        {
            Facing.FrontLeft => FrontLeftRootName,
            Facing.FrontRight => FrontRightRootName,
            Facing.BackLeft => BackLeftRootName,
            Facing.BackRight => BackRightRootName,
            _ => FrontLeftRootName,
        };

        return FindNamedTransform(spriteRoot, rootName);
    }

    private static Transform FindNamedTransform(Transform root, string name)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (transforms[i].name == name)
            {
                return transforms[i];
            }
        }

        return null;
    }

    private readonly struct PartSnapshot
    {
        public PartSnapshot(
            string name,
            Sprite sprite,
            Color color,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector2 size,
            Vector2 pivot,
            Bounds bounds)
        {
            Name = name;
            Sprite = sprite;
            Color = color;
            LocalPosition = localPosition;
            LocalRotation = localRotation;
            Size = size;
            Pivot = pivot;
            Bounds = bounds;
        }

        public string Name { get; }
        public Sprite Sprite { get; }
        public Color Color { get; }
        public Vector3 LocalPosition { get; }
        public Quaternion LocalRotation { get; }
        public Vector2 Size { get; }
        public Vector2 Pivot { get; }
        public Bounds Bounds { get; }
    }
}
