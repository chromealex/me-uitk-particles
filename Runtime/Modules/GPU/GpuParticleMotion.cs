using UnityEngine;

namespace ME.UITK.Particles {

    /// <summary>Shared sampled motion integrals for GPU rendering and particle access. No per-frame simulation.</summary>
    internal sealed class GpuParticleMotion : System.IDisposable {

        private const int samples = 256;
        private const int rows = 16;
        private readonly Color[] data;
        private readonly bool enabled, spatialNoise;
        private readonly Texture2D texture;
        private readonly Vector4 noiseSettings, octaveSettings;
        private float velocityBound, forceBound, noiseBound;

        internal GpuParticleMotion(ParticleSystem system, Material material) {

            var rotation = system.rotationOverLifetime;
            var velocity = system.velocityOverLifetime;
            var force = system.forceOverLifetime;
            var noise = system.noise;
            this.spatialNoise = GpuParticleDynamics.SpatialNoise(system);
            var inheritance = system.inheritVelocity;
            var inheritCurrent = inheritance.enabled == true && inheritance.mode == ParticleSystemInheritVelocityMode.Current;
            var inheritedVelocity = system.transform.InverseTransformVector(system.main.emitterVelocity);
            material.SetVector("_MotionEnabled", new Vector4(rotation.enabled == true ? 1f : 0f, velocity.enabled == true || inheritCurrent == true ? 1f : 0f, force.enabled == true ? 1f : 0f, noise.enabled == true ? (this.spatialNoise == true ? 2f : 1f) : 0f));
            this.enabled = rotation.enabled == true || velocity.enabled == true || force.enabled == true || noise.enabled == true || inheritCurrent == true;
            if (this.enabled == false) {
                return;
            }

            this.data = new Color[samples * rows];
            var damping = noise.damping == true ? 1f / Mathf.Max(.001f, noise.frequency) : 1f;
            this.noiseSettings = new Vector4(noise.enabled == true ? noise.frequency : 0f,
                noise.enabled == true ? noise.scrollSpeed.constant : 0f, noise.enabled == true ? noise.octaveCount : 0, (int)noise.quality);
            this.octaveSettings = new Vector4(noise.octaveMultiplier, noise.octaveScale, 0f, 0f);
            for (var i = 0; i < samples; i++) {
                var age = i / (samples - 1f);
                var r0 = rotation.enabled == true ? rotation.z.Evaluate(age, 0f) : 0f;
                var r1 = rotation.enabled == true ? rotation.z.Evaluate(age, 1f) : 0f;
                this.Set(i, 0, new Color(r0, r1, 0f, 0f));
                for (var endpoint = 0; endpoint < 2; endpoint++) {
                    var v = velocity.enabled == true ? new Vector3(velocity.x.Evaluate(age, endpoint), velocity.y.Evaluate(age, endpoint), velocity.z.Evaluate(age, endpoint)) : Vector3.zero;
                    if (inheritCurrent == true) {
                        v += inheritedVelocity * inheritance.curve.Evaluate(age, endpoint);
                    }

                    var f = force.enabled == true ? new Vector3(force.x.Evaluate(age, endpoint), force.y.Evaluate(age, endpoint), force.z.Evaluate(age, endpoint)) : Vector3.zero;
                    var strength = Vector3.zero;
                    if (noise.enabled == true) {
                        strength = noise.separateAxes == true
                            ? new Vector3(noise.strengthX.Evaluate(age, endpoint), noise.strengthY.Evaluate(age, endpoint), noise.strengthZ.Evaluate(age, endpoint))
                            : Vector3.one * noise.strength.Evaluate(age, endpoint);
                        strength *= damping * noise.positionAmount.constant;
                    }

                    this.Set(i, 1 + endpoint, GpuParticleMotion.ToColor(v));
                    this.Set(i, 5 + endpoint, GpuParticleMotion.ToColor(f));
                    this.Set(i, 9 + endpoint, GpuParticleMotion.ToColor(strength));
                    this.velocityBound = Mathf.Max(this.velocityBound, v.magnitude);
                    this.forceBound = Mathf.Max(this.forceBound, f.magnitude);
                    this.noiseBound = Mathf.Max(this.noiseBound, this.spatialNoise == true
                        ? Mathf.Max(Mathf.Abs(strength.x), Mathf.Abs(strength.y), Mathf.Abs(strength.z)) : strength.magnitude);
                }
            }

            // Preserve rates while integrating acceleration twice and velocity once over normalized age.
            for (var endpoint = 0; endpoint < 2; endpoint++) {
                this.Integrate(1 + endpoint, 3 + endpoint);
                this.Integrate(5 + endpoint, 7 + endpoint);
                this.Integrate(7 + endpoint, 13 + endpoint);
                for (var i = 0; i < samples; i++) {
                    var lo = Mathf.Max(0, i - 1);
                    var hi = Mathf.Min(samples - 1, i + 1);
                    var derivative = (this.Get(hi, 9 + endpoint) - this.Get(lo, 9 + endpoint)) * ((samples - 1f) / (hi - lo));
                    this.Set(i, 11 + endpoint, derivative);
                }
            }

            if (this.spatialNoise == false) {
                this.noiseBound /= 2f * Mathf.PI;
            }
            var integral = Vector2.zero;
            for (var i = 0; i < samples; i++) {
                var rate = this.Get(i, 0);
                if (i > 0) {
                    var previous = this.Get(i - 1, 0);
                    integral += new Vector2(previous.r + rate.r, previous.g + rate.g) * (.5f / (samples - 1));
                }

                this.Set(i, 0, new Color(rate.r, rate.g, integral.x, integral.y));
            }

            this.texture = new Texture2D(samples, rows, TextureFormat.RGBAFloat, false, true) {
                name = "GPU particle motion", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            this.texture.SetPixels(this.data);
            this.texture.Apply(false, true);
            material.SetTexture("_Motion", this.texture);
            material.SetVector("_NoiseSettings", this.noiseSettings);
            material.SetVector("_OctaveSettings", this.octaveSettings);

        }

        private static Color ToColor(Vector3 value) {

            return new Color(value.x, value.y, value.z, 0f);

        }

        private Color Get(int sample, int row) {

            return this.data[row * samples + sample];

        }

        private void Set(int sample, int row, Color value) {

            if (float.IsFinite(value.r) == false || float.IsFinite(value.g) == false || float.IsFinite(value.b) == false || float.IsFinite(value.a) == false) {
                throw new System.ArgumentException("GPU motion curves must contain finite values.");
            }

            this.data[row * samples + sample] = value;

        }

        private void Integrate(int source, int destination) {

            var integral = Color.clear;
            this.Set(0, destination, integral);
            for (var i = 1; i < samples; i++) {
                integral += (this.Get(i - 1, source) + this.Get(i, source)) * (.5f / (samples - 1));
                this.Set(i, destination, integral);
            }

        }

        private Color Sample(float age, int row) {

            var sample = Mathf.Clamp01(age) * (samples - 1);
            var lo = Mathf.FloorToInt(sample);
            return Color.LerpUnclamped(this.Get(lo, row), this.Get(Mathf.Min(lo + 1, samples - 1), row), sample - lo);

        }

        private Vector3 SampleRange(float age, int row, float random) {

            var value = Color.LerpUnclamped(this.Sample(age, row), this.Sample(age, row + 1), random);
            return new Vector3(value.r, value.g, value.b);

        }

        internal static float Random(uint seed) {

            unchecked {
                seed = seed * 747796405u + 2891336453u;
                seed = ((seed >> (int)((seed >> 28) + 4)) ^ seed) * 277803737u;
                seed = (seed >> 22) ^ seed;
            }

            return (seed & 0x00ffffffu) / 16777215f;

        }

        internal void Evaluate(float age, float lifetime, float random, out Vector3 displacement, out Vector3 velocity, out float angle, out float angularVelocity) {

            if (this.enabled == false) {
                displacement = Vector3.zero;
                velocity = Vector3.zero;
                angle = 0f;
                angularVelocity = 0f;
                return;
            }

            displacement = lifetime * this.SampleRange(age, 3, random) + lifetime * lifetime * this.SampleRange(age, 13, random);
            velocity = this.SampleRange(age, 1, random) + lifetime * this.SampleRange(age, 7, random);
            var rotation = this.Sample(age, 0);
            angle = lifetime * Mathf.Lerp(rotation.b, rotation.a, random);
            angularVelocity = Mathf.Lerp(rotation.r, rotation.g, random);
            if (this.noiseSettings.z == 0f || this.spatialNoise == true) {
                return;
            }

            var strength = this.SampleRange(age, 9, random);
            var derivative = this.SampleRange(age, 11, random) / Mathf.Max(lifetime, .000001f);
            var wave = Vector3.zero;
            var slope = Vector3.zero;
            // Convert the approximate wave frequency to angular phase. Keep velocity
            // strength independent of this conversion so turbulence stays bounded.
            var amplitude = 1f;
            var frequency = this.noiseSettings.x * (1f + this.noiseSettings.y) * (2f * Mathf.PI);
            var normalization = 0f;
            for (var octave = 0; octave < (int)this.noiseSettings.z; octave++) {
                var phase = random * 6.2831853f + octave * 1.618034f;
                var rates = new Vector3(1f, 1.17f + this.noiseSettings.w * .11f, 1.43f + this.noiseSettings.w * .13f) * frequency;
                var t = rates * (age * lifetime) + new Vector3(phase, phase + 2.1f, phase + 4.2f);
                wave += new Vector3(Mathf.Sin(t.x), Mathf.Sin(t.y), Mathf.Sin(t.z)) * amplitude;
                slope += Vector3.Scale(new Vector3(Mathf.Cos(t.x), Mathf.Cos(t.y), Mathf.Cos(t.z)), rates) * amplitude;
                normalization += amplitude;
                amplitude *= this.octaveSettings.x;
                frequency *= this.octaveSettings.y;
            }

            wave /= Mathf.Max(normalization, .000001f) * (2f * Mathf.PI);
            slope /= Mathf.Max(normalization, .000001f) * (2f * Mathf.PI);
            displacement += Vector3.Scale(strength, wave);
            velocity += Vector3.Scale(derivative, wave) + Vector3.Scale(strength, slope);

        }

        internal Vector3 BaseVelocity(ParticleSystem.Particle particle) {

            var lifetime = Mathf.Max(particle.startLifetime, .000001f);
            this.Evaluate(1f - particle.remainingLifetime / lifetime, lifetime, GpuParticleMotion.Random(particle.randomSeed),
                out _, out var velocity, out _, out _);
            return particle.velocity - velocity;

        }

        internal float BoundsPadding(float lifetime) {

            // With BaseVelocity removing the initial module rate, only the bounded
            // displacement difference remains. Do not budget noise velocity twice.
            return 2f * (lifetime * this.velocityBound + lifetime * lifetime * this.forceBound)
                + (this.spatialNoise == true ? lifetime * this.noiseBound * this.noiseSettings.x * 4f : 2f * this.noiseBound);

        }

        public void Dispose() {

            ParticleEnvironment.Destroy(this.texture);

        }

    }

}
