using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal sealed class RenderTextureParticleBackend : NativeParticleBackend {

        internal ParticleTexturePresenter presenter { get; private set; }
        private Bounds effectBounds;
        public override Bounds renderBounds => this.presenter != null ? this.presenter.renderBounds : default;

        public RenderTextureParticleBackend(ParticleElement owner) : base(owner) {

        }

        public override void Initialize(GameObject prefab) {

            base.Initialize(prefab);
            foreach (var renderer in this.renderers) {
                renderer.enabled = true;
            }

            this.effectBounds = ParticleBoundsCalculator.Calculate(this.systems, this.renderers, this.instance.transform.position);
            this.presenter = new ParticleTexturePresenter(this.owner, this.instance, this.GetBounds);
            this.presenter.Initialize();

        }

        private Bounds GetBounds() {

            var liveBounds = new Bounds(Vector3.zero, Vector3.one * .1f);
            ParticleBoundsCalculator.Encapsulate(ref liveBounds, this.renderers, this.instance.transform.position);
            if (this.effectBounds.Contains(liveBounds.min) == false || this.effectBounds.Contains(liveBounds.max) == false) {
                liveBounds.Expand(Vector3.Max(liveBounds.size * .1f, Vector3.one * .2f));
                this.effectBounds.Encapsulate(liveBounds);
            }

            return this.effectBounds;

        }

        public override void Play() {

            base.Play();
            this.presenter.Play();

        }

        public override void Stop() {

            base.Stop();
            this.presenter.Stop();

        }

        public override void Tick() {

            this.presenter.PresentTextureFrame();

        }

        public override void Dispose() {

            if (this.presenter != null) {
                this.presenter.Dispose();
                this.presenter = null;
            }

            base.Dispose();

        }

    }

}
