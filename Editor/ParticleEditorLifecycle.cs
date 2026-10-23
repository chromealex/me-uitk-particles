using System.Collections.Generic;
using UnityEditor;

namespace ME.UITK.Particles.Editor {

    [InitializeOnLoad]
    internal static class ParticleEditorLifecycle {

        private static readonly HashSet<ParticleElement> elements = new();

        static ParticleEditorLifecycle() {

            ParticleEnvironment.trackEditorElement = ParticleEditorLifecycle.Track;
            ParticleEnvironment.requestEditorUpdate = EditorApplication.QueuePlayerLoopUpdate;
            EditorApplication.update += ParticleEditorLifecycle.Update;
            AssemblyReloadEvents.beforeAssemblyReload += ParticleEditorLifecycle.Suspend;
            EditorApplication.quitting += ParticleEditorLifecycle.Suspend;
            EditorApplication.playModeStateChanged += ParticleEditorLifecycle.ChangeMode;

        }

        private static void Track(ParticleElement element, bool attached) {

            if (attached == true) {
                ParticleEditorLifecycle.elements.Add(element);
            } else {
                ParticleEditorLifecycle.elements.Remove(element);
            }

        }

        private static void Update() {

            unchecked {
                ParticleEnvironment.editorFrame++;
            }

        }

        private static void Suspend() {

            ParticleEnvironment.editorSuspended = true;
            foreach (var element in new List<ParticleElement>(ParticleEditorLifecycle.elements)) {
                element.Release();
            }

        }

        private static void ChangeMode(PlayModeStateChange state) {

            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode) {
                ParticleEditorLifecycle.Suspend();
                return;
            }

            ParticleEnvironment.editorSuspended = false;
            foreach (var element in new List<ParticleElement>(ParticleEditorLifecycle.elements)) {
                element.RestoreEditorPlayback();
            }

        }

    }

}
