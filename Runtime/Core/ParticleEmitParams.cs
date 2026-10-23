using System;
using UnityEngine;

namespace ME.UITK.Particles {

    /// <summary>Optional overrides for a burst. Null fields retain the prefab's sampled start values.</summary>
    public struct ParticleEmitParams {

        public Vector3? position;
        public Vector3? velocity;
        public Color? startColor;
        public float? startSize;
        public float? startLifetime;

        internal void Validate() {

            if (this.position.HasValue == true) {
                ParticleEmitParams.ValidateVector(this.position.Value, nameof(this.position));
            }
            if (this.velocity.HasValue == true) {
                ParticleEmitParams.ValidateVector(this.velocity.Value, nameof(this.velocity));
            }
            if (this.startSize.HasValue == true && (float.IsFinite(this.startSize.Value) == false || this.startSize.Value < 0f)) {
                throw new ArgumentOutOfRangeException(nameof(this.startSize));
            }
            if (this.startLifetime.HasValue == true && (float.IsFinite(this.startLifetime.Value) == false || this.startLifetime.Value <= 0f)) {
                throw new ArgumentOutOfRangeException(nameof(this.startLifetime));
            }
            if (this.startColor.HasValue == true) {
                var color = this.startColor.Value;
                for (var i = 0; i < 4; i++) {
                    if (float.IsFinite(color[i]) == false || color[i] < 0f || color[i] > 1f) {
                        throw new ArgumentOutOfRangeException(nameof(this.startColor));
                    }
                }
            }

        }

        internal void Apply(ref ParticleSystem.Particle particle) {

            if (this.position.HasValue == true) {
                particle.position = this.position.Value;
            }
            if (this.velocity.HasValue == true) {
                particle.velocity = this.velocity.Value;
            }
            if (this.startColor.HasValue == true) {
                particle.startColor = this.startColor.Value;
            }
            if (this.startSize.HasValue == true) {
                particle.startSize = this.startSize.Value;
            }
            if (this.startLifetime.HasValue == true) {
                particle.startLifetime = this.startLifetime.Value;
                particle.remainingLifetime = this.startLifetime.Value;
            }

        }

        internal ParticleSystem.EmitParams ToNative() {

            var result = new ParticleSystem.EmitParams();
            if (this.position.HasValue == true) {
                result.position = this.position.Value;
                result.applyShapeToPosition = false;
            }
            if (this.velocity.HasValue == true) {
                result.velocity = this.velocity.Value;
            }
            if (this.startColor.HasValue == true) {
                result.startColor = this.startColor.Value;
            }
            if (this.startSize.HasValue == true) {
                result.startSize = this.startSize.Value;
            }
            if (this.startLifetime.HasValue == true) {
                result.startLifetime = this.startLifetime.Value;
            }

            return result;

        }

        private static void ValidateVector(Vector3 value, string name) {

            if (float.IsFinite(value.x) == false || float.IsFinite(value.y) == false || float.IsFinite(value.z) == false) {
                throw new ArgumentOutOfRangeException(name);
            }

        }

    }

}
