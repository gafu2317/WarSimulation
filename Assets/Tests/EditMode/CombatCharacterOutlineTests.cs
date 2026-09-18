using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class CombatCharacterOutlineTests
{
    [Test]
    public void ChangingTeam_PreservesSpriteColorsAndExistingPropertiesIncludingInactiveParts()
    {
        var root = new GameObject("Outline Character");
        root.SetActive(false);
        try
        {
            var character = root.AddComponent<Character>();
            var part = new GameObject("Hidden Facing", typeof(SpriteRenderer));
            part.transform.SetParent(root.transform);
            part.SetActive(false);
            var renderer = part.GetComponent<SpriteRenderer>();
            var original = new Color(0.8f, 0.6f, 0.4f, 0.5f);
            renderer.color = original;
            renderer.renderingLayerMask = 5;
            var properties = new MaterialPropertyBlock();
            properties.SetFloat("_ExistingProperty", 0.75f);
            renderer.SetPropertyBlock(properties);

            character.SetTeam(CombatTeam.Ally);
            renderer.GetPropertyBlock(properties);
            Color ally = properties.GetColor("_CombatOutlineColor");
            Assert.That(ally.b, Is.GreaterThan(ally.r));

            character.SetTeam(CombatTeam.Enemy);
            renderer.GetPropertyBlock(properties);
            Color enemy = properties.GetColor("_CombatOutlineColor");
            Assert.That(enemy.r, Is.GreaterThan(enemy.b));
            Assert.That(renderer.color, Is.EqualTo(original));
            Assert.That(renderer.renderingLayerMask, Is.EqualTo(5u | (1u << 31)));
            Assert.That(properties.GetFloat("_ExistingProperty"), Is.EqualTo(0.75f));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [TestCase("PC")]
    [TestCase("Mobile")]
    public void Render_ColorsOnlyCombinedOuterSilhouetteAndRespectsOcclusion(string quality)
    {
        var previousPipeline = QualitySettings.renderPipeline;
        var previousTarget = RenderTexture.active;
        var root = new GameObject("Outline Render Test");
        var target = new RenderTexture(256, 256, 24);
        var image = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), Vector2.one * 0.5f, 2);
        var material = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
        var occluderMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        try
        {
            QualitySettings.renderPipeline = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(
                $"Assets/Settings/{quality}_RPAsset.asset");
            var cameraObject = new GameObject("Outline Test Camera", typeof(Camera));
            cameraObject.transform.SetParent(root.transform);
            var camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << 30;
            camera.targetTexture = target;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            for (int i = 0; i < 2; i++)
            {
                var part = new GameObject("Part", typeof(SpriteRenderer));
                part.layer = 30;
                part.transform.SetParent(root.transform);
                part.transform.position = new Vector3(i == 0 ? -0.3f : 0.3f, 0, 0);
                var renderer = part.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.sharedMaterial = material;
                renderer.renderingLayerMask = 1u << 31;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_CombatOutlineColor", Color.blue);
                renderer.SetPropertyBlock(properties);
            }

            Capture(camera, target, image);
            Assert.That(CountBlue(image), Is.GreaterThan(100), "The outer silhouette needs a blue border.");
            for (int x = 90; x <= 166; x++)
            {
                Color inside = image.GetPixel(x, 128);
                Assert.That(inside.r, Is.GreaterThan(0.8f), "Overlapping parts must remain white without internal borders.");
            }

            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.SetParent(root.transform);
            wall.layer = 30;
            wall.transform.position = new Vector3(0, 0, -1);
            wall.transform.localScale = new Vector3(3, 3, 0.1f);
            wall.GetComponent<Renderer>().sharedMaterial = occluderMaterial;
            Capture(camera, target, image);
            Assert.That(CountBlue(image), Is.Zero, "Opaque obstacles must hide the silhouette and its outline.");
        }
        finally
        {
            QualitySettings.renderPipeline = previousPipeline;
            RenderTexture.active = previousTarget;
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(sprite);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(occluderMaterial);
        }
    }

    private static void Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render();
        RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
        image.Apply();
    }

    private static int CountBlue(Texture2D image)
    {
        int count = 0;
        foreach (Color32 pixel in image.GetPixels32())
            if (pixel.b > 150 && pixel.r < 80 && pixel.g < 80) count++;
        return count;
    }
}
