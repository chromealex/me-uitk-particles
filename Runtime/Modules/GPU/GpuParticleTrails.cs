using UnityEngine;
using UnityEngine.Rendering;

namespace ME.UITK.Particles {

    // Analytic history strips. No native trail simulation or per-frame mesh uploads.
    internal sealed class GpuParticleTrails : System.IDisposable {

        private readonly ParticleSystem.TrailModule settings;
        private readonly Mesh mesh;
        private readonly Material material;
        private readonly Texture2D lookup;
        private readonly GameObject gameObject;
        private readonly MeshRenderer renderer;
        private float maxWidth;
        private readonly System.Collections.Generic.List<Snapshot> retired = new();
        internal float endTime { get; private set; }

        private struct Snapshot {

            internal ParticleSystem.Particle particle;
            internal float time;

        }

        internal float HistoryLifetime(float particleLifetime) {

            return this.settings.lifetime.constant * particleLifetime;

        }

        internal float TailLifetime(float particleLifetime) {

            return this.settings.dieWithParticles == true ? 0f : this.HistoryLifetime(particleLifetime);

        }

        internal static string Check(ParticleSystem system) {

            var trails = system.trails;
            if (trails.enabled == false) {
                return null;
            }

            if (trails.mode != ParticleSystemTrailMode.PerParticle || trails.worldSpace == true) {
                return "GPU Trails require Per Particle mode and World Space disabled; Ribbon is unsupported";
            }

            if (trails.textureMode != ParticleSystemTrailTextureMode.Stretch || trails.generateLightingData == true) {
                return "GPU Trails require Stretch texture mode and Generate Lighting Data disabled";
            }

            if (trails.lifetime.mode != ParticleSystemCurveMode.Constant || trails.lifetime.constant <= 0f
                || float.IsFinite(trails.lifetime.constant) == false || trails.sizeAffectsLifetime == true) {
                return "GPU Trails require a positive constant Lifetime and Size Affects Lifetime disabled";
            }

            if ((trails.widthOverTrail.mode != ParticleSystemCurveMode.Constant && trails.widthOverTrail.mode != ParticleSystemCurveMode.Curve)
                || GpuParticleTrails.IsGradient(trails.colorOverTrail) == false || GpuParticleTrails.IsGradient(trails.colorOverLifetime) == false) {
                return "GPU Trails support a single Width over Trail curve and single color gradients";
            }

            var material = system.GetComponent<ParticleSystemRenderer>().trailMaterial;
            if (material == null || material.shader == null || material.shader.name != "ParticleUI/Alpha"
                || material.mainTextureScale != Vector2.one || material.mainTextureOffset != Vector2.zero) {
                return "GPU Trails require a ParticleUI/Alpha trail material without UV transforms";
            }

            if (SystemInfo.supports32bitsIndexBuffer == false) {
                return "GPU Trails require 32-bit mesh indices";
            }

            return null;

        }

        private static bool IsGradient(ParticleSystem.MinMaxGradient gradient) {

            return gradient.mode == ParticleSystemGradientMode.Color || gradient.mode == ParticleSystemGradientMode.Gradient;

        }

        internal GpuParticleTrails(ParticleSystem source, GameObject parent, Material particleMaterial) {

            this.settings = source.trails;
            var sourceMaterial = source.GetComponent<ParticleSystemRenderer>().trailMaterial;
            this.material = new Material(particleMaterial) { hideFlags = HideFlags.HideAndDontSave, };
            this.material.EnableKeyword("PARTICLE_TRAILS");
            this.material.mainTexture = sourceMaterial.mainTexture;
            this.material.SetColor("_Color", sourceMaterial.GetColor("_Color"));
            this.material.SetVector("_TrailSettings", new Vector4(this.settings.lifetime.constant, this.settings.sizeAffectsWidth == true ? 1f : 0f,
                this.settings.inheritParticleColor == true ? 1f : 0f, this.settings.dieWithParticles == true ? 1f : 0f));
            this.material.SetVector("_TrailTextureScale", this.settings.textureScale);
            this.lookup = new Texture2D(256, 3, TextureFormat.RGBAFloat, false, true) {
                name = "GPU trail curves", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            for (var i = 0; i < 256; i++) {
                var age = i / 255f;
                var width = Mathf.Abs(this.settings.widthOverTrail.Evaluate(age));
                this.maxWidth = Mathf.Max(this.maxWidth, width);
                this.lookup.SetPixel(i, 0, this.settings.colorOverTrail.Evaluate(age));
                this.lookup.SetPixel(i, 1, this.settings.colorOverLifetime.Evaluate(age));
                this.lookup.SetPixel(i, 2, new Color(width, 0f, 0f, 0f));
            }

            this.lookup.Apply(false, true);
            this.material.SetTexture("_TrailCurves", this.lookup);
            this.mesh = new Mesh { name = "Analytic particle trails", hideFlags = HideFlags.HideAndDontSave, indexFormat = IndexFormat.UInt32, };
            this.gameObject = new GameObject("Analytic trails") { hideFlags = HideFlags.HideAndDontSave, layer = ParticleInstance.EFFECT_LAYER, };
            this.gameObject.transform.SetParent(parent.transform, false);
            this.gameObject.AddComponent<MeshFilter>().sharedMesh = this.mesh;
            this.renderer = this.gameObject.AddComponent<MeshRenderer>();
            this.renderer.sharedMaterial = this.material;
            this.renderer.shadowCastingMode = ShadowCastingMode.Off;
            this.renderer.receiveShadows = false;
            this.renderer.enabled = false;

        }

        internal float WidthPadding(float size) {

            return this.maxWidth * (this.settings.sizeAffectsWidth == true ? Mathf.Abs(size) : 1f);

        }

        internal System.Action CaptureState() {

            var snapshots = this.retired.ToArray();
            var end = this.endTime;
            return () => {
                this.retired.Clear();
                this.retired.AddRange(snapshots);
                this.endTime = end;
            };

        }

        internal void Clear() {

            this.retired.Clear();
            this.endTime = 0f;

        }

        internal void Retain(ParticleSystem.Particle particle, float time) {

            var tailLifetime = this.TailLifetime(particle.startLifetime);
            if (tailLifetime <= 0f || particle.remainingLifetime + tailLifetime <= 0f) {
                return;
            }

            // Bound retained geometry under repeated short-lived manual bursts.
            if (this.retired.Count >= 16000) {
                this.retired.RemoveAt(0);
            }

            this.retired.Add(new Snapshot { particle = particle, time = time, });
            this.endTime = Mathf.Max(this.endTime, time + particle.remainingLifetime + tailLifetime);

        }

        internal void ExpandBounds(ref Bounds bounds, float now, GpuParticleMotion motion, Vector3 gravity) {

            for (var i = this.retired.Count - 1; i >= 0; i--) {
                var snapshot = this.retired[i];
                if (snapshot.time + snapshot.particle.remainingLifetime + this.TailLifetime(snapshot.particle.startLifetime) <= now) {
                    this.retired.RemoveAt(i);
                    continue;
                }

                var particle = snapshot.particle;
                var extent = motion.BaseVelocity(particle).magnitude * particle.startLifetime + gravity.magnitude * particle.startLifetime * particle.startLifetime
                    + motion.BoundsPadding(particle.startLifetime) + this.WidthPadding(GpuParticleEmitter.Width(particle));
                bounds.Encapsulate(new Bounds(particle.position, Vector3.one * extent * 2f));
            }

        }

        internal void Rebuild(ParticleSystem.Particle[] particles, int count, float stateTime, Bounds bounds, GpuParticleMotion motion, Vector3 gravity) {

            using var vertexLease = UnityEngine.Pool.ListPool<Vector3>.Get(out var vertices);
            using var colorLease = UnityEngine.Pool.ListPool<Color32>.Get(out var colors);
            using var uvLease = UnityEngine.Pool.ListPool<Vector2>.Get(out var uv);
            using var motionLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var velocities);
            using var sizeLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var sizes);
            using var ageLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var ages);
            using var timeLease = UnityEngine.Pool.ListPool<Vector2>.Get(out var times);
            using var indexLease = UnityEngine.Pool.ListPool<int>.Get(out var indices);
            for (var i = 0; i < count + this.retired.Count; i++) {
                var particle = i < count ? particles[i] : this.retired[i - count].particle;
                var sampleTime = i < count ? stateTime : this.retired[i - count].time;
                var random = GpuParticleMotion.Random(particle.randomSeed);
                if (this.settings.ratio <= 0f || this.settings.ratio < 1f && random >= this.settings.ratio) {
                    continue;
                }

                var historyLifetime = this.HistoryLifetime(particle.startLifetime);
                var length = particle.velocity.magnitude * historyLifetime
                    + gravity.magnitude * particle.startLifetime * historyLifetime + motion.BoundsPadding(particle.startLifetime);
                var segments = Mathf.Clamp(Mathf.CeilToInt(length / Mathf.Max(.001f, this.settings.minVertexDistance)), 2, 32);
                var first = vertices.Count;
                for (var segment = 0; segment <= segments; segment++) {
                    for (var side = 0; side < 2; side++) {
                        vertices.Add(particle.position);
                        times.Add(new Vector2(sampleTime, 0f));
                        colors.Add(particle.startColor);
                        uv.Add(new Vector2(segment / (float)segments, side));
                        velocities.Add(new Vector4(particle.velocity.x, particle.velocity.y, particle.velocity.z, particle.remainingLifetime));
                        sizes.Add(GpuParticleEmitter.SizeRotation(particle));
                        ages.Add(new Vector4(particle.startLifetime, particle.startLifetime - particle.remainingLifetime,
                            particle.angularVelocity * Mathf.Deg2Rad, random));
                    }

                    if (segment < segments) {
                        var vertex = first + segment * 2;
                        indices.Add(vertex);
                        indices.Add(vertex + 1);
                        indices.Add(vertex + 2);
                        indices.Add(vertex + 1);
                        indices.Add(vertex + 3);
                        indices.Add(vertex + 2);
                    }
                }
            }

            this.mesh.Clear();
            this.mesh.SetVertices(vertices);
            this.mesh.SetColors(colors);
            this.mesh.SetUVs(0, uv);
            this.mesh.SetUVs(1, velocities);
            this.mesh.SetUVs(2, sizes);
            this.mesh.SetUVs(3, ages);
            this.mesh.SetUVs(4, times);
            this.mesh.SetTriangles(indices, 0);
            this.mesh.bounds = bounds;
            this.material.SetFloat("_StateTime", stateTime);

        }

        internal void SetLifetime(Texture texture) {

            this.material.SetTexture("_Lifetime", texture);

        }

        internal void SetTime(float elapsed) {

            this.material.SetFloat("_Elapsed", elapsed);

        }

        internal void SetEnabled(bool enabled) {

            this.renderer.enabled = enabled;

        }

        public void Dispose() {

            ParticleEnvironment.Destroy(this.gameObject);
            ParticleEnvironment.Destroy(this.mesh);
            ParticleEnvironment.Destroy(this.material);
            ParticleEnvironment.Destroy(this.lookup);

        }

    }

}
