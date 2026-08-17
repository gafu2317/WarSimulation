using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using UnityCliBridge.Handlers;

namespace UnityCliBridge.Tests
{
    [TestFixture]
    public class AssetManagementHandlerTests
    {
        private const string TestFolder = "Assets/UnityCliBridgeTests/AnimatorControllers";

        [SetUp]
        public void Setup()
        {
            EnsureFolder("Assets/UnityCliBridgeTests");
            EnsureFolder(TestFolder);
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder("Assets/UnityCliBridgeTests"))
            {
                AssetDatabase.DeleteAsset("Assets/UnityCliBridgeTests");
                AssetDatabase.Refresh();
            }
        }

        [Test]
        public void CreateAnimatorController_WithStatesParametersAndTransitions_ReturnsSuccess()
        {
            string idlePath = CreateClipAsset("Idle");
            string runPath = CreateClipAsset("Run");
            string controllerPath = TestFolder + "/Hero.controller";

            var parameters = new JObject
            {
                ["controllerPath"] = controllerPath,
                ["parameters"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "isMoving",
                        ["type"] = "Bool",
                        ["defaultBool"] = false
                    }
                },
                ["states"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "Idle",
                        ["motionPath"] = idlePath
                    },
                    new JObject
                    {
                        ["name"] = "Run",
                        ["motionPath"] = runPath
                    }
                },
                ["defaultState"] = "Idle",
                ["transitions"] = new JArray
                {
                    new JObject
                    {
                        ["from"] = "Idle",
                        ["to"] = "Run",
                        ["conditions"] = new JArray
                        {
                            new JObject
                            {
                                ["parameter"] = "isMoving",
                                ["mode"] = "If"
                            }
                        }
                    }
                }
            };

            JObject result = ToJObject(AssetManagementHandler.CreateAnimatorController(parameters));

            Assert.IsNull(result.Value<string>("error"));
            Assert.IsTrue(result.Value<bool>("success"));
            Assert.AreEqual(controllerPath, result.Value<string>("controllerPath"));
            Assert.AreEqual("Idle", result.Value<string>("defaultState"));
            Assert.AreEqual(1, result.Value<int>("parameterCount"));
            Assert.AreEqual(2, result.Value<int>("stateCount"));
            Assert.AreEqual(1, result.Value<int>("transitionCount"));

            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            Assert.IsNotNull(controller);
            Assert.AreEqual(1, controller.parameters.Length);
            Assert.AreEqual("isMoving", controller.parameters[0].name);
            Assert.AreEqual(AnimatorControllerParameterType.Bool, controller.parameters[0].type);
            Assert.IsFalse(controller.parameters[0].defaultBool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = FindState(stateMachine, "Idle");
            AnimatorState runState = FindState(stateMachine, "Run");

            Assert.IsNotNull(idleState);
            Assert.IsNotNull(runState);
            Assert.AreEqual("Idle", stateMachine.defaultState.name);
            Assert.AreEqual(idlePath, AssetDatabase.GetAssetPath(idleState.motion));
            Assert.AreEqual(runPath, AssetDatabase.GetAssetPath(runState.motion));

            AnimatorStateTransition transition = idleState.transitions.Single();
            Assert.AreEqual(runState, transition.destinationState);
            Assert.AreEqual(1, transition.conditions.Length);
            Assert.AreEqual("isMoving", transition.conditions[0].parameter);
            Assert.AreEqual(AnimatorConditionMode.If, transition.conditions[0].mode);
        }

        [Test]
        public void CreateAnimatorController_WithExistingPathAndOverwriteFalse_ReturnsError()
        {
            string controllerPath = TestFolder + "/Existing.controller";
            AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            JObject result = ToJObject(AssetManagementHandler.CreateAnimatorController(new JObject
            {
                ["controllerPath"] = controllerPath
            }));

            Assert.AreEqual("AnimatorController request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "AnimatorController already exists",
                result["validationErrors"]?[0]?.ToString()
            );
        }

        [Test]
        public void CreateAnimatorController_WithMissingMotionClip_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateAnimatorController(new JObject
            {
                ["controllerPath"] = TestFolder + "/MissingClip.controller",
                ["states"] = new JArray
                {
                    new JObject
                    {
                        ["name"] = "Idle",
                        ["motionPath"] = TestFolder + "/Missing.anim"
                    }
                }
            }));

            Assert.AreEqual("AnimatorController request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "does not point to a Motion asset",
                result["validationErrors"]?[0]?.ToString()
            );
        }

        [Test]
        public void CreateAnimatorController_WithDuplicateStateNames_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateAnimatorController(new JObject
            {
                ["controllerPath"] = TestFolder + "/DuplicateState.controller",
                ["states"] = new JArray
                {
                    new JObject { ["name"] = "Idle" },
                    new JObject { ["name"] = "Idle" }
                }
            }));

            Assert.AreEqual("AnimatorController request validation failed", result.Value<string>("error"));
            StringAssert.Contains("Duplicate state name: Idle", result["validationErrors"]?.ToString());
        }

        [Test]
        public void CreateAnimatorController_WithUnknownTransitionParameter_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateAnimatorController(new JObject
            {
                ["controllerPath"] = TestFolder + "/UnknownParameter.controller",
                ["states"] = new JArray
                {
                    new JObject { ["name"] = "Idle" },
                    new JObject { ["name"] = "Run" }
                },
                ["transitions"] = new JArray
                {
                    new JObject
                    {
                        ["from"] = "Idle",
                        ["to"] = "Run",
                        ["conditions"] = new JArray
                        {
                            new JObject
                            {
                                ["parameter"] = "isMoving",
                                ["mode"] = "If"
                            }
                        }
                    }
                }
            }));

            Assert.AreEqual("AnimatorController request validation failed", result.Value<string>("error"));
            StringAssert.Contains("references unknown parameter: isMoving", result["validationErrors"]?.ToString());
        }

        [Test]
        public void CreateAnimationClip_WithSpriteFramesAndLoopTime_ReturnsSuccess()
        {
            string idle0 = CreateSpriteAsset("Idle_0");
            string idle1 = CreateSpriteAsset("Idle_1");
            string clipPath = TestFolder + "/Hero.anim";

            JObject result = ToJObject(AssetManagementHandler.CreateAnimationClip(new JObject
            {
                ["clipPath"] = clipPath,
                ["spritePaths"] = new JArray { idle0, idle1 },
                ["frameRate"] = 12f,
                ["loopTime"] = true,
                ["bindingPath"] = ""
            }));

            Assert.IsNull(result.Value<string>("error"));
            Assert.IsTrue(result.Value<bool>("success"));
            Assert.AreEqual(2, result.Value<int>("frameCount"));
            Assert.AreEqual(12f, result.Value<float>("frameRate"));
            Assert.IsTrue(result.Value<bool>("loopTime"));

            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Assert.IsNotNull(clip);
            Assert.AreEqual(12f, clip.frameRate);

            var binding = new EditorCurveBinding
            {
                path = string.Empty,
                type = typeof(SpriteRenderer),
                propertyName = "m_Sprite"
            };
            ObjectReferenceKeyframe[] keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.IsNotNull(keyframes);
            Assert.AreEqual(2, keyframes.Length);
            Assert.AreEqual(idle0, AssetDatabase.GetAssetPath((Sprite)keyframes[0].value));
            Assert.AreEqual(idle1, AssetDatabase.GetAssetPath((Sprite)keyframes[1].value));

            var serializedClip = new SerializedObject(clip);
            var clipSettings = serializedClip.FindProperty("m_AnimationClipSettings");
            Assert.IsNotNull(clipSettings);
            Assert.IsTrue(clipSettings.FindPropertyRelative("m_LoopTime").boolValue);
        }

        [Test]
        public void CreateAnimationClip_WithExistingPathAndOverwriteFalse_ReturnsError()
        {
            string clipPath = TestFolder + "/Existing.anim";
            AssetDatabase.CreateAsset(new AnimationClip(), clipPath);

            JObject result = ToJObject(AssetManagementHandler.CreateAnimationClip(new JObject
            {
                ["clipPath"] = clipPath,
                ["spritePaths"] = new JArray { CreateSpriteAsset("ExistingSprite") }
            }));

            Assert.AreEqual("AnimationClip request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "AnimationClip already exists",
                result["validationErrors"]?[0]?.ToString()
            );
        }

        [Test]
        public void CreateAnimationClip_WithMissingSprite_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateAnimationClip(new JObject
            {
                ["clipPath"] = TestFolder + "/MissingSprite.anim",
                ["spritePaths"] = new JArray { TestFolder + "/Missing.png" }
            }));

            Assert.AreEqual("AnimationClip request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "must resolve to an existing Sprite asset",
                result["validationErrors"]?.ToString()
            );
        }

        [Test]
        public void CreateAnimationClip_WithInvalidFrameRate_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateAnimationClip(new JObject
            {
                ["clipPath"] = TestFolder + "/InvalidFrameRate.anim",
                ["spritePaths"] = new JArray { CreateSpriteAsset("FrameRateSprite") },
                ["frameRate"] = 0
            }));

            Assert.AreEqual("AnimationClip request validation failed", result.Value<string>("error"));
            StringAssert.Contains("frameRate must be greater than 0", result["validationErrors"]?.ToString());
        }

        [Test]
        public void CreateSpriteAtlas_WithPackableFolderAndSettings_ReturnsSuccess()
        {
            string atlasPath = TestFolder + "/UI.spriteatlas";
            JObject result = ToJObject(AssetManagementHandler.CreateSpriteAtlas(new JObject
            {
                ["atlasPath"] = atlasPath,
                ["packables"] = new JArray { TestFolder },
                ["packingSettings"] = new JObject
                {
                    ["padding"] = 4,
                    ["allowRotation"] = false,
                    ["tightPacking"] = true
                },
                ["textureSettings"] = new JObject
                {
                    ["filterMode"] = "Bilinear",
                    ["generateMipMaps"] = false
                }
            }));

            Assert.IsNull(result.Value<string>("error"));
            Assert.IsTrue(result.Value<bool>("success"));
            Assert.AreEqual(1, result.Value<int>("packableCount"));

            SpriteAtlas atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
            Assert.IsNotNull(atlas);

            Object[] packables = SpriteAtlasExtensions.GetPackables(atlas);
            Assert.AreEqual(1, packables.Length);
            Assert.AreEqual(TestFolder, AssetDatabase.GetAssetPath(packables[0]));

            SpriteAtlasPackingSettings packingSettings = SpriteAtlasExtensions.GetPackingSettings(atlas);
            Assert.AreEqual(4, packingSettings.padding);
            Assert.IsFalse(packingSettings.enableRotation);
            Assert.IsTrue(packingSettings.enableTightPacking);

            SpriteAtlasTextureSettings textureSettings = SpriteAtlasExtensions.GetTextureSettings(atlas);
            Assert.AreEqual(FilterMode.Bilinear, textureSettings.filterMode);
            Assert.IsFalse(textureSettings.generateMipMaps);
        }

        [Test]
        public void CreateSpriteAtlas_WithExistingPathAndOverwriteFalse_ReturnsError()
        {
            string atlasPath = TestFolder + "/Existing.spriteatlas";
            AssetDatabase.CreateAsset(new SpriteAtlas(), atlasPath);

            JObject result = ToJObject(AssetManagementHandler.CreateSpriteAtlas(new JObject
            {
                ["atlasPath"] = atlasPath
            }));

            Assert.AreEqual("SpriteAtlas request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "SpriteAtlas already exists",
                result["validationErrors"]?[0]?.ToString()
            );
        }

        [Test]
        public void CreateSpriteAtlas_WithInvalidPackablePath_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateSpriteAtlas(new JObject
            {
                ["atlasPath"] = TestFolder + "/InvalidPackable.spriteatlas",
                ["packables"] = new JArray { TestFolder + "/MissingFolder" }
            }));

            Assert.AreEqual("SpriteAtlas request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "must point to an existing folder, Sprite, or Texture2D asset",
                result["validationErrors"]?[0]?.ToString()
            );
        }

        [Test]
        public void CreateSpriteAtlas_WithInvalidFilterMode_ReturnsError()
        {
            JObject result = ToJObject(AssetManagementHandler.CreateSpriteAtlas(new JObject
            {
                ["atlasPath"] = TestFolder + "/InvalidFilter.spriteatlas",
                ["textureSettings"] = new JObject
                {
                    ["filterMode"] = "Nearest"
                }
            }));

            Assert.AreEqual("SpriteAtlas request validation failed", result.Value<string>("error"));
            StringAssert.Contains(
                "textureSettings.filterMode must be one of Point, Bilinear, Trilinear",
                result["validationErrors"]?.ToString()
            );
        }

        [Test]
        public void CreateMaterial_WithOverwriteTrue_ReplacesExistingMaterial()
        {
            string materialPath = TestFolder + "/Overwrite.mat";

            JObject createInitial = ToJObject(AssetManagementHandler.CreateMaterial(new JObject
            {
                ["materialPath"] = materialPath,
                ["shader"] = FindShaderName("Universal Render Pipeline/Lit", "Standard"),
                ["properties"] = new JObject
                {
                    ["color"] = new JObject
                    {
                        ["r"] = 1f,
                        ["g"] = 0f,
                        ["b"] = 0f,
                        ["a"] = 1f
                    }
                }
            }));
            Assert.IsNull(createInitial.Value<string>("error"));

            JObject overwrite = ToJObject(AssetManagementHandler.CreateMaterial(new JObject
            {
                ["materialPath"] = materialPath,
                ["shader"] = FindShaderName("Universal Render Pipeline/Unlit", "Unlit/Color"),
                ["overwrite"] = true
            }));

            Assert.IsNull(overwrite.Value<string>("error"));
            Assert.IsTrue(overwrite.Value<bool>("success"));

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsNotNull(material);
            StringAssert.Contains("Unlit", material.shader.name);
        }

        [Test]
        public void ModifyMaterial_WithShaderOnly_Succeeds()
        {
            string materialPath = TestFolder + "/ShaderOnly.mat";
            JObject created = ToJObject(AssetManagementHandler.CreateMaterial(new JObject
            {
                ["materialPath"] = materialPath,
                ["shader"] = FindShaderName("Universal Render Pipeline/Lit", "Standard")
            }));
            Assert.IsNull(created.Value<string>("error"));

            JObject modified = ToJObject(AssetManagementHandler.ModifyMaterial(new JObject
            {
                ["materialPath"] = materialPath,
                ["shader"] = FindShaderName("Universal Render Pipeline/Unlit", "Unlit/Color")
            }));

            Assert.IsNull(modified.Value<string>("error"));
            Assert.IsTrue(modified.Value<bool>("success"));
            Assert.IsTrue(modified.Value<bool>("shaderChanged"));

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            Assert.IsNotNull(material);
            StringAssert.Contains("Unlit", material.shader.name);
        }

        private static JObject ToJObject(object result)
        {
            return result as JObject ?? JObject.FromObject(result);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = path.Substring(0, path.LastIndexOf('/'));
            string folderName = path.Substring(path.LastIndexOf('/') + 1);
            AssetDatabase.CreateFolder(parent, folderName);
        }

        private static string CreateSpriteAsset(string name)
        {
            string path = $"{TestFolder}/{name}.png";
            string absolutePath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                path
            );
            var texture = new Texture2D(4, 4);
            texture.SetPixels(new[]
            {
                Color.red, Color.red, Color.red, Color.red,
                Color.red, Color.red, Color.red, Color.red,
                Color.red, Color.red, Color.red, Color.red,
                Color.red, Color.red, Color.red, Color.red,
            });
            texture.Apply();
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.IsNotNull(sprite);
            return path;
        }

        private static string CreateClipAsset(string name)
        {
            string path = $"{TestFolder}/{name}.anim";
            var clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            AssetDatabase.SaveAssets();
            return path;
        }

        private static AnimatorState FindState(AnimatorStateMachine stateMachine, string stateName)
        {
            return stateMachine.states
                .Select(child => child.state)
                .SingleOrDefault(state => state.name == stateName);
        }

        private static string FindShaderName(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate) && Shader.Find(candidate) != null)
                {
                    return candidate;
                }
            }

            Assert.Fail("Expected at least one test shader to exist.");
            return null;
        }
    }
}
