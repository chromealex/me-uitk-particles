using UnityEngine;
using UnityEngine.Rendering;

namespace ME.UITK.Particles {

    // Editor hooks are installed by the editor assembly; runtime has no UnityEditor dependency.
    internal static class ParticleEnvironment {

        internal static System.Action<ParticleElement, bool> trackEditorElement;
        internal static System.Action requestEditorUpdate;
        internal static bool editorSuspended;
        internal static uint editorFrame;

        internal static double Now(bool unscaled) {

            return Application.isPlaying == false ? Time.realtimeSinceStartupAsDouble
                : unscaled == true ? Time.unscaledTimeAsDouble : Time.timeAsDouble;

        }

        internal static void Destroy(Object value) {

            if (value == null) {
                return;
            }

            if (Application.isPlaying == true && ParticleEnvironment.editorSuspended == false) {
                Object.Destroy(value);
            } else {
                Object.DestroyImmediate(value);
            }

        }

        internal static void Render(Camera camera) {

            if (GraphicsSettings.currentRenderPipeline == null) {
                camera.Render();
                return;
            }

            var request = new RenderPipeline.StandardRequest {
                destination = camera.targetTexture,
            };
            if (RenderPipeline.SupportsRenderRequest(camera, request) == false) {
                throw new System.NotSupportedException("The current render pipeline does not support particle editor previews.");
            }

            RenderPipeline.SubmitRenderRequest(camera, request);

        }

    }

}
