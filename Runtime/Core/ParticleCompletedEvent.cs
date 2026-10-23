// <summary>
// Defines the pooled UI Toolkit playback completion event with trickle-down and bubbling support.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    /// <summary>Dispatched once per playback when its completion conditions pass.</summary>
    public sealed class ParticleCompletedEvent : EventBase<ParticleCompletedEvent> {

        public ParticleCompletedEvent() {

            this.Init();

        }

        protected override void Init() {

            base.Init();
            this.bubbles = true;
            this.tricklesDown = true;

        }

    }

}
