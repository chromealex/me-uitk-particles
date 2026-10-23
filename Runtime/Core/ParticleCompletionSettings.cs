// <summary>
// Defines and validates playback completion thresholds, condition matching, clock selection
// and optional stopping when completion conditions are met.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System;

namespace ME.UITK.Particles {

    public enum ParticleCompletionMatch {
        Any,
        All,
    }

    public enum ParticleCompletionClock {
        Unscaled,
        Scaled,
    }

    /// <summary>Disabled thresholds mean natural completion. Settings are captured at Play().</summary>
    [Serializable]
    public struct ParticleCompletionSettings {

        public bool timeEnabled;
        public float timeGreaterThan;
        public bool countEnabled;
        public uint countLessThan;
        public ParticleCompletionMatch match;
        public ParticleCompletionClock clock;
        public bool stopOnCompletion;

        internal void Validate() {

            if (this.timeEnabled == true && (float.IsNaN(this.timeGreaterThan) == true || float.IsInfinity(this.timeGreaterThan) == true || this.timeGreaterThan < 0)) {
                throw new ArgumentOutOfRangeException(nameof(this.timeGreaterThan));
            }

            if (this.countEnabled == true && this.countLessThan < 1) {
                throw new ArgumentOutOfRangeException(nameof(this.countLessThan));
            }

            if (this.match != ParticleCompletionMatch.Any && this.match != ParticleCompletionMatch.All) {
                throw new ArgumentOutOfRangeException(nameof(this.match));
            }

            if (this.clock != ParticleCompletionClock.Unscaled && this.clock != ParticleCompletionClock.Scaled) {
                throw new ArgumentOutOfRangeException(nameof(this.clock));
            }

        }

    }

}
