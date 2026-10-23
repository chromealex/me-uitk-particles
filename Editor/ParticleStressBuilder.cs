// <summary>
// Creates a dedicated stress-test scene, seeded particle prefab and UI panel assets.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System.IO;
using System.Linq;
using ME.UITK.Particles.Demo;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace ME.UITK.Particles.Editor {

    public static class ParticleStressBuilder {

        private const string Folder = "Assets/ME.UITK.Particles/Demo/Stress";
        public const string ScenePath = "Assets/ME.UITK.Particles/Demo/ParticleStressTest.unity";

        [MenuItem("Tools/ME.UITK.Particles/Build and Open Stress Test")]
        public static void Build() {

            if (EditorApplication.isPlayingOrWillChangePlaymode == true) {
                Debug.LogWarning("Exit Play Mode before building the stress test.");
                return;
            }

            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() == false) {
                return;
            }

            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            const string generated = "Assets/ME.UITK.Particles/Demo/Generated/";
            var source = new GameObject("StressParticles", typeof(ParticleSystem));
            var system = source.GetComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 30;
            main.startLifetime = 30;
            main.startSpeed = 0;
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .075f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.15f, .85f, 1f), new Color(1f, .35f, .1f));
            main.maxParticles = 1000;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            system.useAutoRandomSeed = false;
            system.randomSeed = 12345;
            var emission = system.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, new ParticleSystem.MinMaxCurve(1000)), });
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(12, 6, .01f);
            var noise = system.noise;
            noise.enabled = true;
            noise.strength = .15f;
            noise.frequency = .3f;
            noise.scrollSpeed = .3f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var renderer = source.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(generated + "ParticleAlpha.mat");
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/StressParticles.prefab");
            Object.DestroyImmediate(source);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(Folder + "/StressPanel.asset");
            if (panel == null) {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, Folder + "/StressPanel.asset");
            }

            panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(generated + "RuntimeTheme.tss");
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1440, 900);
            panel.match = .5f;
            EditorUtility.SetDirty(panel);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -12);
            camera.orthographic = true;
            camera.cullingMask = ~(1 << 31);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.025f, .035f, .06f);
            var events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var module = events.GetComponent<InputSystemUIInputModule>();
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
            var document = new GameObject("Stress UI");
            document.SetActive(false);
            document.AddComponent<UIDocument>().panelSettings = panel;
            var stress = document.AddComponent<ParticleStressTest>();
            stress.styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/ME.UITK.Particles/Demo/ParticleDemo.uss");
            stress.layout = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ME.UITK.Particles/Demo/ParticleStressTest.uxml");
            stress.cellTemplate = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/ME.UITK.Particles/Demo/ParticleStressCell.uxml");
            stress.particlePrefab = prefab;
            stress.vfxGraphPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/VFXGraph Stress.prefab");
            document.SetActive(true);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Particle stress test created: " + ScenePath);

        }

    }

}
