using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    // Compare presentation paths without replacing or restarting the native simulation.
    internal sealed class AutoParticleBackend : MeshParticleBackend {

        internal bool isSelecting { get; private set; }
        private ParticleTexturePresenter presenter;
        internal ParticleAtlasPresenter atlasPresenter { get; private set; }
        private bool atlasCompatible;
        private readonly bool allowAtlas;
        private uint sampledJobThreshold;
        private Bounds bounds;
        private bool textureMode, playing, sampledMesh, sampledTexture, sampledJobs;
        private uint sampledCount;
        private float sampledDensity;
        private Vector2 sampledSize;
        private double warmupEnd, sampleStart, meshTime, textureTime;
        private int sampleStartFrame;

        internal AutoParticleBackend(ParticleElement owner, bool allowAtlas = true) : base(owner) {

            this.allowAtlas = allowAtlas;

        }

        public override Bounds renderBounds => this.textureMode == true ? (this.atlasPresenter != null ? this.atlasPresenter.renderBounds : this.bounds) : base.renderBounds;

        public override void Initialize(GameObject prefab) {

            base.Initialize(prefab);
            this.atlasCompatible = this.allowAtlas == true && AtlasParticleCompatibility.Check(prefab) == null;

        }

        public override void Play() {

            base.Play();
            this.playing = true;
            this.sampledCount = 0u;
            this.isSelecting = false;
            this.SetTextureMode(false);

        }

        public override void Stop() {

            base.Stop();
            this.playing = false;
            this.isSelecting = false;
            if (this.presenter != null) {
                this.presenter.Stop();
                this.presenter.Suspend();
            }

            foreach (var renderer in this.renderers) {
                renderer.enabled = false;
            }

            if (this.atlasPresenter != null) {
                this.atlasPresenter.Suspend();
            }

            this.bakedVertexCount = 0u;

        }

        public override void Tick() {

            if (this.playing == false) {
                return;
            }

            var count = this.particleCount;
            var size = this.owner.contentRect.size;
            if (count == 0u) {
                this.sampledCount = 0u;
                this.isSelecting = false;
                this.SetTextureMode(false);
            } else if (this.sampledCount == 0u || count > (ulong)this.sampledCount * 2u || (ulong)count * 2u < this.sampledCount
                || this.sampledJobThreshold != this.owner.jobVertexThreshold || this.sampledJobs != this.owner.useJobs || this.sampledDensity != this.owner.effectivePixelsPerUnit || this.sampledSize != size) {
                this.sampledCount = count;
                this.sampledJobs = this.owner.useJobs;
                this.sampledJobThreshold = this.owner.jobVertexThreshold;
                this.sampledDensity = this.owner.effectivePixelsPerUnit;
                this.sampledSize = size;
                this.sampledMesh = this.sampledTexture = false;
                this.isSelecting = true;
                this.StartSample(false);
            }

            if (this.isSelecting == true) {
                this.UpdateSelection();
            }

            if (this.textureMode == true) {
                if (this.atlasPresenter != null) {
                    this.atlasPresenter.Tick();
                } else {
                    this.presenter.PresentTextureFrame();
                }
            } else {
                base.Tick();
            }

        }

        private void StartSample(bool texture) {

            this.SetTextureMode(texture);
            // Exclude resource creation, pipeline warm-up and frames from the previous path.
            this.warmupEnd = Time.realtimeSinceStartupAsDouble + .5;
            this.sampleStartFrame = -1;
            this.owner.SetAutoMode(texture == true ? ParticleRenderMode.RenderTexture : ParticleRenderMode.Mesh, "Auto: comparing Mesh and RenderTexture frame time");

        }

        private void UpdateSelection() {

            var now = Time.realtimeSinceStartupAsDouble;
            if (Application.isFocused == false || this.owner.isPaused == true || this.owner.contentRect.width <= 0f || this.owner.contentRect.height <= 0f) {
                this.warmupEnd = now + .5;
                this.sampleStartFrame = -1;
                return;
            }

            if (now < this.warmupEnd) {
                return;
            }

            if (this.sampleStartFrame < 0) {
                this.sampleStart = now;
                this.sampleStartFrame = Time.frameCount;
                return;
            }

            var frames = Time.frameCount - this.sampleStartFrame;
            if (now - this.sampleStart < 1.0 || frames < 12) {
                return;
            }

            var average = (now - this.sampleStart) / frames;
            if (this.textureMode == true) {
                this.textureTime = average;
                this.sampledTexture = true;
            } else {
                this.meshTime = average;
                this.sampledMesh = true;
            }

            if (this.sampledMesh == false || this.sampledTexture == false) {
                this.StartSample(this.sampledMesh == true);
                return;
            }

            this.CompleteSelection(this.meshTime, this.textureTime);

        }

        internal void CompleteSelection(double mesh, double textureFrame) {

            this.meshTime = mesh;
            this.textureTime = textureFrame;
            this.isSelecting = false;
            // A small difference is noise (or a frame cap); keep the cheaper resource footprint.
            var texture = this.textureTime < this.meshTime * .95;
            this.SetTextureMode(texture);
            this.owner.SetAutoMode(this.activePresentationMode,
                $"Auto: Mesh {this.meshTime * 1000:F2} ms, RT {this.textureTime * 1000:F2} ms/frame at {this.sampledCount} particles");

        }

        private Bounds GetBounds() {

            var live = new Bounds(Vector3.zero, Vector3.one * .1f);
            ParticleBoundsCalculator.Encapsulate(ref live, this.renderers, this.instance.transform.position);
            live.Expand(Vector3.Max(live.size * .1f, Vector3.one * .2f));
            this.bounds.Encapsulate(live);
            return this.bounds;

        }

        private ParticleRenderMode activePresentationMode => this.textureMode == false ? ParticleRenderMode.Mesh
            : this.atlasPresenter != null && this.atlasPresenter.page != null ? ParticleRenderMode.RenderTextureAtlas : ParticleRenderMode.RenderTexture;

        private void SetTextureMode(bool texture) {

            this.textureMode = texture;
            if (texture == true && this.atlasCompatible == true) {
                if (this.atlasPresenter == null) {
                    // Share the existing simulation; presimulation would restart a live effect.
                    this.bounds = new Bounds(Vector3.zero, Vector3.one * .1f);
                    this.atlasPresenter = new ParticleAtlasPresenter(this.owner, this.instance, this.renderers, true, this.GetBounds());
                }

                // Benchmark individual RT first, then group only after RT wins over Mesh.
                this.atlasPresenter.groupingEnabled = this.isSelecting == false;
                this.atlasPresenter.Play();
            } else {
                if (this.atlasPresenter != null) {
                    this.atlasPresenter.Suspend();
                }

                if (texture == true && this.presenter == null) {
                    this.bounds = new Bounds(Vector3.zero, Vector3.one * .1f);
                    this.presenter = new ParticleTexturePresenter(this.owner, this.instance, this.GetBounds);
                    this.presenter.Initialize();
                }

                foreach (var renderer in this.renderers) {
                    renderer.enabled = texture == true && this.playing == true;
                }

                if (this.presenter != null) {
                    if (texture == true && this.playing == true) {
                        this.presenter.Play();
                    } else {
                        this.presenter.Suspend();
                    }
                }
            }

            this.bakedVertexCount = 0u;
            this.owner.SetAutoMode(this.activePresentationMode, "Auto: waiting for stable particle count");
            this.owner.MarkDirtyRepaint();

        }

        public override void Draw(MeshGenerationContext context) {

            if (this.textureMode == false) {
                base.Draw(context);
            } else if (this.atlasPresenter != null) {
                this.atlasPresenter.Draw(context);
            }

        }

        public override void Dispose() {

            if (this.atlasPresenter != null) {
                this.atlasPresenter.Dispose();
                this.atlasPresenter = null;
            }

            if (this.presenter != null) {
                this.presenter.Dispose();
                this.presenter = null;
            }

            base.Dispose();

        }

    }

}
