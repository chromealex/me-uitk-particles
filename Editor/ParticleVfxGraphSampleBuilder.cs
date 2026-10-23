using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using Object = UnityEngine.Object;

namespace ME.UITK.Particles.Editor {

    public static class ParticleVfxGraphSampleBuilder {

        private const string sampleFolder = "Assets/ME.UITK.Particles/Demo/VFXGraph";

        [MenuItem("Tools/ME.UITK.Particles/Create VFXGraph Sample")]
        public static void CreateSample() {

            Directory.CreateDirectory(ParticleVfxGraphSampleBuilder.sampleFolder);
            var graphPath = ParticleVfxGraphSampleBuilder.sampleFolder + "/VFXGraph Burst.vfx";
            if (File.Exists(graphPath) == false) {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.visualeffectgraph");
                File.Copy(Path.Combine(package.resolvedPath, "Editor/Templates/Simple_Burst.vfx"), graphPath);
                AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var prefabPath = ParticleVfxGraphSampleBuilder.sampleFolder + "/VFXGraph Burst.prefab";
            if (File.Exists(prefabPath) == false) {
                var source = new GameObject("VFXGraph Burst");
                try {
                    source.AddComponent<VisualEffect>().visualEffectAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                    PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
                } finally {
                    Object.DestroyImmediate(source);
                }
            }

            AssetDatabase.Refresh();

        }

    }

}
