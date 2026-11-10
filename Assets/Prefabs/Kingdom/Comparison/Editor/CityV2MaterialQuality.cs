using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WarSimulation.CountryComparison.Editor
{
    public static class CityV2MaterialQuality
    {
        const string Root = "Assets/Prefabs/Kingdom/CityV2/Materials/";

        public static bool IsDedicated(string name) => name is "Carved_Limestone" or "Bronze_Fittings" or "Glazing_Blue" or "Glazing_Amber"
            or "Glazing_Sage" or "Glazing_Plum" or "Coal" or "Glowing_Coals" or "Leather" or "Wicker" or "Architectural_Glass" || name.StartsWith("Identity_", StringComparison.Ordinal);

        [MenuItem("WarSim/Country/Quality Review/Apply Material Improvements")]
        public static void Apply()
        {
            Tone(Existing("Cobble"), new Color(.76f, .77f, .75f, .14f), .20f, .18f);
            Tone(Existing("Fieldstone"), new Color(.88f, .88f, .85f, .48f), .22f, .18f);
            Tone(Existing("Limestone"), new Color(.91f, .91f, .87f, .36f), .20f, .18f);
            Tone(Existing("Oak"), new Color(.75f, .68f, .59f, .66f), .25f, .20f);
            Tone(Clone("Carved_Limestone", "Plaster"), new Color(.78f, .80f, .79f, .14f), .23f, .10f);
            Tone(Clone("Bronze_Fittings", "Brass"), new Color(.70f, .61f, .42f, .70f), .5f, .1f);
            Plain("Glazing_Blue", new Color(.19f, .37f, .43f), .78f, .15f);
            Plain("Glazing_Amber", new Color(.46f, .36f, .20f), .73f, .1f);
            Plain("Glazing_Sage", new Color(.24f, .39f, .33f), .73f, .1f);
            Plain("Glazing_Plum", new Color(.35f, .24f, .37f), .75f, .1f);
            Plain("Coal", new Color(.045f, .043f, .04f), .13f, 0);
            Plain("Leather", new Color(.24f, .12f, .065f), .22f, 0);
            Plain("Wicker", new Color(.40f, .28f, .13f), .18f, 0);
            Plain("Identity_Steel", new Color(.45f, .51f, .54f), .40f, .55f);
            Plain("Identity_Ivory", new Color(.90f, .84f, .65f), .25f, 0);
            Plain("Identity_Ink", new Color(.065f, .040f, .052f), .20f, 0);
            Plain("Identity_Healing", new Color(.10f, .48f, .29f), .36f, .08f);
            var spiritLeaf = Clone("Identity_SpiritLeaf", "Foliage");
            foreach (var property in new[] { "_BaseMap", "_BumpMap" }) spiritLeaf.SetTexture(property, Existing("Foliage").GetTexture(property));
            spiritLeaf.EnableKeyword("_NORMALMAP");
            Tone(spiritLeaf, new Color(.78f, 1.03f, .85f, .60f), .16f, .12f);
            Luminous("Identity_Crystal", new Color(.18f, .50f, .69f), new Color(.09f, .24f, .36f));
            Luminous("Identity_SpiritLight", new Color(.55f, .88f, .72f), new Color(.28f, .48f, .32f));
            Luminous("Identity_Lantern", new Color(.83f, .22f, .20f), new Color(.38f, .09f, .035f));
            Plain("Glowing_Coals", new Color(.20f, .045f, .008f), .15f, 0);
            var embers = Existing("Glowing_Coals");
            embers.EnableKeyword("_EMISSION");
            embers.SetColor("_EmissionColor", new Color(.75f, .12f, .012f));
            var glass = Plain("Architectural_Glass", new Color(.33f, .58f, .62f, .32f), .87f, .08f);
            glass.SetFloat("_Surface", 1);
            glass.SetFloat("_ZWrite", 0);
            glass.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            glass.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            glass.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            glass.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            glass.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            glass.SetOverrideTag("RenderType", "Transparent");
            glass.renderQueue = (int)RenderQueue.Transparent;
            glass.SetShaderPassEnabled("ShadowCaster", false);
            glass.SetShaderPassEnabled("DepthOnly", false);
            glass.SetShaderPassEnabled("DepthNormals", false);
            AssetDatabase.SaveAssets();
        }

        static Material Existing(string name) => AssetDatabase.LoadAssetAtPath<Material>(Root + name + ".mat")
            ?? throw new InvalidOperationException("Missing new-city material: " + name);

        static Material Clone(string name, string source)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + name + ".mat");
            if (material == null)
            {
                material = new Material(Existing(source)) { name = name };
                AssetDatabase.CreateAsset(material, Root + name + ".mat");
            }
            return material;
        }

        static void Tone(Material material, Color tint, float smoothness, float bump)
        {
            Undo.RecordObject(material, "Improve new-city surface material");
            material.shader = Shader.Find("WarSimulation/City Surface Tone") ?? throw new InvalidOperationException("Surface tone shader missing.");
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_BumpScale", bump);
            material.DisableKeyword("_EMISSION");
            EditorUtility.SetDirty(material);
        }

        static Material Plain(string name, Color colour, float smoothness, float metallic)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + name + ".mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
                AssetDatabase.CreateAsset(material, Root + name + ".mat");
            }
            Undo.RecordObject(material, "Improve new-city functional material");
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        static void Luminous(string name, Color colour, Color emission)
        {
            var material = Plain(name, colour, .48f, .05f);
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
        }
    }
}
