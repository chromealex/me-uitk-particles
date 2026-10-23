// <summary>
// Creates and opens the particle playground scene, sample prefabs, materials and UI panel assets.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System.IO;
using System.Linq;
using UnityEngine.InputSystem;
using ME.UITK.Particles.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace ME.UITK.Particles.Editor {

    public static class ParticleDemoBuilder {

        private const string Folder = "Assets/ME.UITK.Particles/Demo/Generated";
        public const string ScenePath = "Assets/ME.UITK.Particles/Demo/ParticlePlayground.unity";
        [MenuItem("Tools/ME.UITK.Particles/Open Demo")]
        public static void Open() {

            if (EditorApplication.isPlayingOrWillChangePlaymode == false && EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == true) {
                EditorSceneManager.OpenScene(ScenePath);
            }

        }

        [MenuItem("Tools/ME.UITK.Particles/Build and Open Demo")]
        public static void Build() {

            if (EditorApplication.isPlayingOrWillChangePlaymode == true) {
                Debug.LogWarning("Exit Play Mode before rebuilding the demo.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) {
                return;
            }

            ParticleVfxGraphSampleBuilder.CreateSample();
            Directory.CreateDirectory(ParticleDemoBuilder.Folder);
            AssetDatabase.Refresh();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var texture = ParticleDemoBuilder.MakeTexture();
            var material = new Material(Shader.Find("ParticleUI/Alpha")) {
                mainTexture = texture,
            };
            ParticleDemoBuilder.SaveAsset(material, ParticleDemoBuilder.Folder + "/ParticleAlpha.mat");
            var resolve = new Material(Shader.Find("Hidden/ParticleUI/Unpremultiply"));
            ParticleDemoBuilder.SaveAsset(resolve, ParticleDemoBuilder.Folder + "/ResolveAlpha.mat"); // Keep shader referenced in builds.
            var prefabs = ParticleDemoBuilder.MakePresets(material);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -12);
            camera.orthographic = true;
            camera.orthographicSize = 5.3f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .042f, .075f);
            camera.cullingMask = ~(1 << 31);
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var eventSystem = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = eventSystem.GetComponent<InputSystemUIInputModule>();
            const string actionsPath = "Assets/ME.UITK.Particles/Demo/InputSystem_Actions.inputactions";
            module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(actionsPath);
            var references = AssetDatabase.LoadAllAssetsAtPath(actionsPath).OfType<InputActionReference>().ToArray();
            InputActionReference Action(string name) => references.First(r => r.action.actionMap.name == "UI" && r.action.name == name);
            module.point = Action("Point");
            module.leftClick = Action("Click");
            module.rightClick = Action("RightClick");
            module.middleClick = Action("MiddleClick");
            module.scrollWheel = Action("ScrollWheel");
            module.move = Action("Navigate");
            module.submit = Action("Submit");
            module.cancel = Action("Cancel");
            module.trackedDevicePosition = Action("TrackedDevicePosition");
            module.trackedDeviceOrientation = Action("TrackedDeviceOrientation");
            var input = eventSystem.AddComponent<PanelInputConfiguration>();
            input.processWorldSpaceInput = true;
            input.defaultEventCameraIsMainCamera = true;
            input.maxInteractionDistance = 100;
            var theme = ParticleDemoBuilder.MakeTheme();
            var overlay = ParticleDemoBuilder.MakePanel("OverlayPanel", theme, false);
            var world = ParticleDemoBuilder.MakePanel("WorldPanel", theme, true);
            ParticleDemoBuilder.MakeDocument("Overlay UI", overlay, prefabs, false);
            var worldObject = ParticleDemoBuilder.MakeDocument("World UI", world, prefabs, true);
            worldObject.transform.position = new Vector3(0, 0.1f, 0);
            worldObject.transform.rotation = Quaternion.Euler(18, -12, -3);
            worldObject.transform.localScale = Vector3.one;
            var document = worldObject.GetComponent<UIDocument>();
            document.worldSpaceSizeMode = WorldSpaceSizeMode.Fixed;
            document.worldSpaceSize = new Vector2(650, 680);
            var hitbox = worldObject.AddComponent<BoxCollider>();
            hitbox.size = new Vector3(6.5f, 6.8f, .05f);
            hitbox.isTrigger = true;
            // Native world-space input is routed by the panel's auto-created input components.
            var light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(40, -25, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "World stage";
            floor.transform.position = new Vector3(0, -3.6f, 1.5f);
            floor.transform.localScale = new Vector3(8, .15f, 4);
            var floorMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            floorMaterial.color = new Color(.08f, .16f, .21f);
            ParticleDemoBuilder.SaveAsset(floorMaterial, ParticleDemoBuilder.Folder + "/Stage.mat");
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleDemoBuilder.Folder + "/Stage.mat");
            EditorSceneManager.SaveScene(scene, ParticleDemoBuilder.ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Particle UI demo created: " + ParticleDemoBuilder.ScenePath);

        }

        [MenuItem("Tools/ME.UITK.Particles/Update Demo Presets")]
        public static void UpdatePresets() {

            if (EditorApplication.isPlayingOrWillChangePlaymode == true) {
                Debug.LogWarning("Exit Play Mode before updating demo presets.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) {
                return;
            }

            var scene = EditorSceneManager.OpenScene(ParticleDemoBuilder.ScenePath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(ParticleDemoBuilder.Folder + "/ParticleAlpha.mat");
            var prefabs = ParticleDemoBuilder.MakePresets(material);
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var demo in root.GetComponentsInChildren<ParticleDemo>(true)) {
                    demo.prefabs = prefabs;
                    EditorUtility.SetDirty(demo);
                }
            }

            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Updated demo presets: " + prefabs.Length);

        }

        private static GameObject[] MakePresets(Material material) {

            ParticleDemoBuilder.MakeSpriteSheet();

            return new[]
            {
                ParticleDemoBuilder.MakePrefab("Sparks", 0, material),
                ParticleDemoBuilder.MakePrefab("Confetti", 1, material),
                ParticleDemoBuilder.MakePrefab("Trails", 2, material),
                ParticleDemoBuilder.MakePrefab("Sub Emitters", 3, material),
                ParticleDemoBuilder.MakePrefab("Color over Lifetime", 4, material),
                ParticleDemoBuilder.MakePrefab("Size Pulse", 5, material),
                ParticleDemoBuilder.MakePrefab("Rotation over Lifetime", 6, material),
                ParticleDemoBuilder.MakePrefab("Velocity Drift", 7, material),
                ParticleDemoBuilder.MakePrefab("Force over Lifetime", 8, material),
                ParticleDemoBuilder.MakePrefab("Noise Turbulence", 9, material),
                ParticleDemoBuilder.MakePrefab("Limit Velocity", 10, material),
                ParticleDemoBuilder.MakePrefab("Box Rain", 11, material),
                ParticleDemoBuilder.MakePrefab("Emission Waves", 12, material),
                ParticleDemoBuilder.MakePrefab("Texture Sheet Animation", 13, material),
                ParticleDemoBuilder.MakePrefab("Texture Sprite Atlas", 14, material),
            };

        }

        private static void SaveAsset(Object asset, string path) {

            var existing = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (existing != null) {
                EditorUtility.CopySerialized(asset, existing);
                Object.DestroyImmediate(asset);
            } else {
                AssetDatabase.CreateAsset(asset, path);
            }

        }

        private static ThemeStyleSheet MakeTheme() {

            var path = ParticleDemoBuilder.Folder + "/RuntimeTheme.tss";
            File.WriteAllText(path, "@import url(\"unity-theme://default\");\n");
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(path);

        }

        private static PanelSettings MakePanel(string name, ThemeStyleSheet theme, bool world) {

            var panel = ScriptableObject.CreateInstance<PanelSettings>();
            panel.themeStyleSheet = theme;
            panel.renderMode = world == true ? PanelRenderMode.WorldSpace : PanelRenderMode.ScreenSpaceOverlay;
            panel.scaleMode = world == true ? PanelScaleMode.ConstantPixelSize : PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1440, 900);
            panel.match = .5f;
            ParticleDemoBuilder.SaveAsset(panel, ParticleDemoBuilder.Folder + "/" + name + ".asset");
            return AssetDatabase.LoadAssetAtPath<PanelSettings>(ParticleDemoBuilder.Folder + "/" + name + ".asset");

        }

        private static GameObject MakeDocument(string name, PanelSettings panel, GameObject[] prefabs, bool world) {

            var go = new GameObject(name);
            go.SetActive(false);
            go.AddComponent<UIDocument>().panelSettings = panel;
            var demo = go.AddComponent<ParticleDemo>();
            demo.prefabs = prefabs;
            demo.vfxGraphPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ME.UITK.Particles/Demo/VFXGraph/VFXGraph Burst.prefab");
            demo.worldSpace = world;
            demo.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ME.UITK.Particles/Demo/ParticleDemo.uss");
            demo.layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ME.UITK.Particles/Demo/ParticleDemo.uxml");
            demo.clickTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ME.UITK.Particles/Demo/ParticleClick.uxml");
            go.SetActive(true);
            return go;

        }

        private static Texture2D MakeTexture() {

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (var y = 0; y < size; y++) {
                for (var x = 0; x < size; x++) {
                    var radius = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                    texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.5f)));
                }
            }

            texture.Apply();
            var path = ParticleDemoBuilder.Folder + "/ParticleDot.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

        }

        private static void MakeSpriteSheet() {

            const int frameSize = 64;
            var texture = new Texture2D(frameSize * 4, frameSize, TextureFormat.RGBA32, false);
            for (var frame = 0; frame < 4; frame++) {
                for (var y = 0; y < frameSize; y++) {
                    for (var x = 0; x < frameSize; x++) {
                        var point = new Vector2(x + .5f, y + .5f) / frameSize * 2f - Vector2.one;
                        var radius = point.magnitude;
                        var angle = Mathf.Atan2(point.y, point.x);
                        var edge = .55f + .18f * Mathf.Cos(angle * (frame + 3));
                        var alpha = Mathf.Clamp01((edge - radius) * frameSize);
                        var tint = Color.HSVToRGB(frame / 4f, .65f, 1f);
                        tint.a = alpha;
                        texture.SetPixel(frame * frameSize + x, y, tint);
                    }
                }
            }

            texture.Apply();
            var path = ParticleDemoBuilder.Folder + "/ParticleSpriteSheet.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var material = new Material(Shader.Find("ParticleUI/Alpha")) {
                mainTexture = texture,
            };
            material.name = "ParticleSpriteSheet";
            ParticleDemoBuilder.SaveAsset(material, ParticleDemoBuilder.Folder + "/ParticleSpriteSheet.mat");
            for (var frame = 0; frame < 4; frame++) {
                var sprite = Sprite.Create(texture, new Rect(frame * frameSize, 0, frameSize, frameSize),
                    new Vector2(.5f, .5f), frameSize, 0, SpriteMeshType.FullRect);
                sprite.name = "ParticleSprite" + frame;
                ParticleDemoBuilder.SaveAsset(sprite, ParticleDemoBuilder.Folder + "/" + sprite.name + ".asset");
            }

        }

        private static GameObject MakePrefab(string name, uint kind, Material material) {

            // Reload in case a previous generated asset was updated in place.
            material = AssetDatabase.LoadAssetAtPath<Material>(ParticleDemoBuilder.Folder + "/ParticleAlpha.mat");
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleDemoBuilder.Configure(ps, material, kind);
            if (kind == 3) {
                var child = new GameObject("Death sparks");
                child.transform.SetParent(go.transform, false);
                var sub = child.AddComponent<ParticleSystem>();
                sub.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                ParticleDemoBuilder.Configure(sub, material, 0);
                var main = sub.main;
                main.startLifetime = .55f;
                main.startSpeed = 1.2f;
                main.startSize = .13f;
                var emission = sub.emission;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0, 9), });
                var parentMain = ps.main;
                parentMain.startLifetime = .65f;
                var parentEmission = ps.emission;
                parentEmission.SetBursts(new[] { new ParticleSystem.Burst(0, 8), });
                var subs = ps.subEmitters;
                subs.enabled = true;
                subs.AddSubEmitter(sub, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);
            }

            var path = ParticleDemoBuilder.Folder + "/" + name + ".prefab";
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            return prefab;

        }

        private static void Configure(ParticleSystem ps, Material material, uint kind) {

            ps.useAutoRandomSeed = false;
            ps.randomSeed = 12345;
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 3;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.6f, 2.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(.12f, .25f);
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.useUnscaledTime = true;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.2f, 1, .8f), new Color(.9f, .65f, .2f));
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)(kind == 1 ? 80 : 45)), });
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = .1f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1), },
                    new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .4f), new GradientAlphaKey(0, 1), });
            color.color = gradient;
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            if (kind == 1) {
                main.gravityModifier = .18f;
                main.startColor = new ParticleSystem.MinMaxGradient(new Color(1, .25f, .6f), new Color(.35f, .65f, 1));
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-4, 4);
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = .3f;
                noise.frequency = .5f;
            }

            if (kind == 2) {
                main.startSize = .12f;
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = .65f;
                var trails = ps.trails;
                trails.enabled = true;
                trails.lifetime = .35f;
                trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
                renderer.trailMaterial = material;
            }

            // Each additional preset isolates a module while retaining the same baseline burst.
            if (kind == 4) {
                main.startColor = Color.white;
                gradient.SetKeys(new[] {
                    new GradientColorKey(new Color(.1f, 1f, 1f), 0),
                    new GradientColorKey(new Color(.8f, .2f, 1f), .5f),
                    new GradientColorKey(new Color(1f, .45f, .1f), 1),
                }, gradient.alphaKeys);
                color.color = gradient;
            }

            if (kind == 5) {
                main.startSize = .4f;
                size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(
                    new Keyframe(0, .1f), new Keyframe(.3f, 1), new Keyframe(.6f, .25f),
                    new Keyframe(.8f, .7f), new Keyframe(1, 0)));
            }

            if (kind == 6) {
                // An elongated billboard makes rotation visible even with the round dot texture.
                main.startSize3D = true;
                main.startSizeX = .12f;
                main.startSizeY = .45f;
                main.startSizeZ = .12f;
                var rotation = ps.rotationOverLifetime;
                rotation.enabled = true;
                rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            }

            if (kind == 7) {
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = .8f;
                velocity.y = .5f;
            }

            if (kind == 8) {
                var force = ps.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.Local;
                force.x = .8f;
                force.y = -1.4f;
            }

            if (kind == 9) {
                main.startSpeed = .7f;
                var noise = ps.noise;
                noise.enabled = true;
                noise.strength = .8f;
                noise.frequency = 1.2f;
                noise.scrollSpeed = .6f;
            }

            if (kind == 10) {
                main.startSpeed = 4f;
                var limit = ps.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = .5f;
                limit.dampen = .25f;
            }

            if (kind == 11) {
                shape.shapeType = ParticleSystemShapeType.Box;
                shape.scale = new Vector3(3.2f, .15f, .01f);
                shape.position = new Vector3(0, 1.2f, 0);
                main.startSpeed = 0;
                main.startSize = .1f;
                var velocity = ps.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.y = -1.5f;
            }

            if (kind == 12) {
                main.startLifetime = 1.2f;
                emission.SetBursts(new[] {
                    new ParticleSystem.Burst(0, 20),
                    new ParticleSystem.Burst(.45f, 30),
                    new ParticleSystem.Burst(.9f, 40),
                });
            }

            if (kind == 13 || kind == 14) {
                main.startColor = Color.white;
                main.startSize = .5f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(.4f, 1.4f);
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, .5f));
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(ParticleDemoBuilder.Folder + "/ParticleSpriteSheet.mat");
                var sheet = ps.textureSheetAnimation;
                sheet.enabled = true;
                sheet.uvChannelMask = UnityEngine.Rendering.UVChannelFlags.UV0;
                sheet.rowMode = ParticleSystemAnimationRowMode.Custom;
                sheet.frameOverTime = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0, 1, 1));
                sheet.cycleCount = 3;
                if (kind == 13) {
                    sheet.mode = ParticleSystemAnimationMode.Grid;
                    sheet.numTilesX = 4;
                    sheet.numTilesY = 1;
                    sheet.animation = ParticleSystemAnimationType.WholeSheet;
                } else {
                    sheet.mode = ParticleSystemAnimationMode.Sprites;
                    for (var frame = 0; frame < 4; frame++) {
                        sheet.AddSprite(AssetDatabase.LoadAssetAtPath<Sprite>(ParticleDemoBuilder.Folder + "/ParticleSprite" + frame + ".asset"));
                    }
                }
            }

        }

    }

}