using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    /// <summary>Pooled notification for a completed transfer or particles captured by an attractor.</summary>
    public sealed class ParticleArrivedEvent : EventBase<ParticleArrivedEvent> {

        public VisualElement destination { get; internal set; }
        public uint arrivedCount { get; internal set; }
        public int systemIndex { get; internal set; }
        public Vector2 localPosition { get; internal set; }

        public ParticleArrivedEvent() {

            this.Init();

        }

        protected override void Init() {

            base.Init();
            this.bubbles = true;
            this.tricklesDown = true;
            this.destination = null;
            this.arrivedCount = 0;
            this.systemIndex = 0;
            this.localPosition = default;

        }

    }

}
