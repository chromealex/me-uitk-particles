using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles.Editor {

    public sealed class ParticleEditorPreview : EditorWindow {

        private ParticleElement particles;
        private ObjectField prefab;
        private EnumFlagsField mode;

        [MenuItem("Tools/ME.UITK.Particles/Editor Preview")]
        public static void Open() {

            GetWindow<ParticleEditorPreview>("Particle Preview");

        }

        public void CreateGUI() {

            this.particles?.Dispose();
            this.rootVisualElement.Clear();
            this.prefab = new ObjectField("Effect prefab") { objectType = typeof(GameObject), allowSceneObjects = false, };
            this.mode = new EnumFlagsField("Render mode", ParticleRenderMode.Auto);
            this.rootVisualElement.Add(this.prefab);
            this.rootVisualElement.Add(this.mode);
            this.rootVisualElement.Add(new Button(this.Play) { text = "Play / Restart", });
            this.rootVisualElement.Add(new Button(() => this.particles.Stop()) { text = "Stop", });
            this.particles = new ParticleElement();
            this.particles.style.flexGrow = 1;
            this.particles.style.minHeight = 240;
            this.rootVisualElement.Add(this.particles);

        }

        private void Play() {

            this.particles.SetPrefab(this.prefab.value as GameObject, (ParticleRenderMode)this.mode.value);
            this.particles.Play();

        }

        private void OnDisable() {

            this.particles?.Dispose();

        }

    }

}
