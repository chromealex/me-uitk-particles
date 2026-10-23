using UnityEngine;

namespace ME.UITK.Particles {

    // A UI glow analogue, not scene illumination. No Light components are instantiated.
    internal sealed class GpuParticleLights : System.IDisposable {

        private readonly GameObject gameObject;
        private readonly MeshFilter filter;
        private readonly MeshRenderer renderer;
        private readonly Material material;

        internal static string Check(ParticleSystem ps) {

            var lights = ps.lights;
            if (lights.enabled == true && (lights.light == null || lights.light.type != LightType.Point || lights.light.shadows != LightShadows.None)) {
                return "GPU Lights are UI glow: assign a Point Light without shadows; scene lighting and shadows are unavailable";
            }

            return null;

        }

        internal GpuParticleLights(ParticleSystem ps, GameObject parent, Material source) {

            this.material = new Material(source) { hideFlags = HideFlags.HideAndDontSave, };
            this.material.EnableKeyword("PARTICLE_GLOW");
            var lights = ps.lights;
            this.material.SetVector("_LightSettings", new Vector4(lights.light.range, lights.light.intensity, lights.ratio, lights.maxLights));
            this.material.SetVector("_LightOptions", new Vector4(lights.useParticleColor == true ? 1f : 0f, lights.sizeAffectsRange == true ? 1f : 0f,
                lights.alphaAffectsIntensity == true ? 1f : 0f, lights.useRandomDistribution == true ? 1f : 0f));
            this.material.SetColor("_LightColor", lights.light.color);
            this.gameObject = new GameObject("GPU particle glow") { hideFlags = HideFlags.HideAndDontSave, layer = parent.layer, };
            this.gameObject.transform.SetParent(parent.transform, false);
            this.filter = this.gameObject.AddComponent<MeshFilter>();
            this.renderer = this.gameObject.AddComponent<MeshRenderer>();
            this.renderer.sharedMaterial = this.material;
            this.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            this.renderer.receiveShadows = false;
            this.renderer.enabled = false;

        }

        internal void UpdateMesh(Mesh mesh, Texture lifetime, float stateTime) {

            this.filter.sharedMesh = mesh;
            this.material.SetTexture("_Lifetime", lifetime);
            this.material.SetFloat("_StateTime", stateTime);

        }

        internal void SetTime(float time) {

            this.material.SetFloat("_Elapsed", time);

        }

        internal void SetEnabled(bool enabled) {

            this.renderer.enabled = enabled;

        }

        public void Dispose() {

            ParticleEnvironment.Destroy(this.gameObject);
            ParticleEnvironment.Destroy(this.material);

        }

    }

}
