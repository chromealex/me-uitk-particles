using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal sealed class AtlasParticleBackend : NativeParticleBackend {

        private readonly bool automatic;
        private readonly bool allowFallback;
        private ParticleAtlasPresenter presenter;
        internal bool eligible => this.presenter.eligible;
        internal ParticleAtlasPage page => this.presenter.page;
        internal ParticleTexturePresenter fallback => this.presenter.fallback;
        public override Bounds renderBounds => this.presenter.renderBounds;

        internal AtlasParticleBackend(ParticleElement owner, bool automatic, bool allowFallback = true) : base(owner) {

            this.automatic = automatic;
            this.allowFallback = allowFallback;

        }

        public override void Initialize(GameObject prefab) {

            base.Initialize(prefab);
            var bounds = ParticleBoundsCalculator.Calculate(this.systems, this.renderers, this.instance.transform.position);
            this.presenter = new ParticleAtlasPresenter(this.owner, this.instance, this.renderers, this.automatic, bounds, this.allowFallback);

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

            this.presenter.Tick();

        }

        public override void Draw(MeshGenerationContext context) {

            this.presenter.Draw(context);

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
