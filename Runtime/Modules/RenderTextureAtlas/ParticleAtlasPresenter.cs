using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal sealed class ParticleAtlasPresenter : System.IDisposable {

        private readonly ParticleElement owner;
        private readonly GameObject instance;
        private readonly ParticleSystemRenderer[] renderers;
        private bool registered;
        private readonly bool allowFallback;
        internal bool groupingEnabled = true;
        internal readonly bool automatic;
        internal bool playing;
        internal ParticleAtlasPage page;
        internal int slot;
        internal Vector2Int tilePixels;
        internal Vector2 span;
        internal bool eligible;
        internal Bounds bounds;
        internal bool hasFrame;
        private Bounds completedBounds;
        private Bounds estimatedBounds;
        private Vector3 originalPosition;
        private float[] minimumSizes, maximumSizes;
        internal ParticleTexturePresenter fallback { get; private set; }
        public Bounds renderBounds => this.bounds;
        internal GameObject effectObject => this.instance;
        internal ParticleElement element => this.owner;

        internal ParticleAtlasPresenter(ParticleElement owner, GameObject instance, ParticleSystemRenderer[] renderers, bool automatic, Bounds initialBounds, bool allowFallback = true) {

            this.owner = owner;
            this.instance = instance;
            this.renderers = renderers;
            this.automatic = automatic;
            this.allowFallback = allowFallback;
            this.estimatedBounds = initialBounds;
            this.originalPosition = this.instance.transform.position;
            this.minimumSizes = new float[this.renderers.Length];
            this.maximumSizes = new float[this.renderers.Length];
            for (var i = 0; i < this.renderers.Length; i++) {
                this.minimumSizes[i] = this.renderers[i].minParticleSize;
                this.maximumSizes[i] = this.renderers[i].maxParticleSize;
                this.renderers[i].enabled = false;
            }

            this.UpdateBounds();
            this.Resume();

        }

        internal void UpdateBounds() {

            var live = new Bounds(Vector3.zero, Vector3.one * .1f);
            ParticleBoundsCalculator.Encapsulate(ref live, this.renderers, this.instance.transform.position);
            this.estimatedBounds.Encapsulate(live);
            this.bounds = this.estimatedBounds;
            var extent = new Vector2(this.bounds.size.x, this.bounds.size.y) * 1.05f + Vector2.one * .1f;
            this.span = new Vector2(ParticleAtlasPresenter.Bucket(extent.x), ParticleAtlasPresenter.Bucket(extent.y));
            var requested = this.span * Mathf.Max(1f, this.owner.effectivePixelsPerUnit);
            this.eligible = this.groupingEnabled == true && float.IsFinite(requested.x) == true && float.IsFinite(requested.y) == true
                && requested.x <= 1024f && requested.y <= 1024f && this.span.x <= 128f && this.span.y <= 128f && this.bounds.size.z <= 128f
                && SystemInfo.maxTextureSize >= 2048;
            this.tilePixels = this.eligible == true
                ? new Vector2Int(Mathf.Max(64, Mathf.CeilToInt(requested.x / 64f) * 64), Mathf.Max(64, Mathf.CeilToInt(requested.y / 64f) * 64))
                : Vector2Int.zero;

        }

        private static float Bucket(float extent) {

            return Mathf.Max(.5f, Mathf.Ceil(extent * 2f) * .5f);

        }

        internal void UseFallback() {

            if (this.page == null && this.fallback != null) {
                return;
            }

            if (this.page != null) {
                this.page.Remove(this);
                this.page = null;
            }

            this.hasFrame = false;
            this.instance.transform.position = this.originalPosition;
            for (var i = 0; i < this.renderers.Length; i++) {
                this.renderers[i].minParticleSize = this.minimumSizes[i];
                this.renderers[i].maxParticleSize = this.maximumSizes[i];
                this.renderers[i].enabled = this.playing;
            }

            if (this.allowFallback == false) {
                foreach (var renderer in this.renderers) {
                    renderer.enabled = false;
                }

                this.owner.SetAutoMode(ParticleRenderMode.None, "Atlas tile exceeds device or size limits; RenderTexture is excluded by the mask.");
                this.owner.MarkDirtyRepaint();
                return;
            }

            if (this.fallback == null) {
                this.fallback = new ParticleTexturePresenter(this.owner, this.instance, () => this.bounds);
                this.fallback.Initialize();
            }

            if (this.playing == true) {
                this.fallback.Play();
            }

            this.owner.SetAtlasMode(false);
            this.owner.MarkDirtyRepaint();

        }

        internal void UseAtlas(ParticleAtlasPage value, int index) {

            if (this.fallback != null) {
                this.fallback.Suspend();
            }

            this.page = value;
            this.slot = index;
            this.hasFrame = false;
            this.owner.SetAtlasMode(true);
            this.PrepareRender();
            this.owner.MarkDirtyRepaint();

        }

        internal void PrepareRender() {

            if (this.page == null) {
                return;
            }

            // Recheck native bounds immediately before culling. Never draw outside the assigned tile.
            this.UpdateBounds();
            var fits = this.playing == true && this.eligible == true && this.span == this.page.span && this.tilePixels == this.page.tilePixels;
            for (var i = 0; i < this.renderers.Length; i++) {
                this.renderers[i].enabled = fits;
                var ratio = this.bounds.size.y / (this.page.span.y * 2f);
                this.renderers[i].minParticleSize = this.minimumSizes[i] * ratio;
                this.renderers[i].maxParticleSize = this.maximumSizes[i] * ratio;
            }

            if (fits == false) {
                this.hasFrame = false;
                return;
            }

            this.instance.transform.position = this.page.TileCenter(this.slot) - this.bounds.center;

        }

        internal void CompleteFrame() {

            if (this.playing == true && this.span == this.page.span && this.tilePixels == this.page.tilePixels) {
                this.completedBounds = new Bounds(this.bounds.center, new Vector3(this.page.span.x, this.page.span.y, this.bounds.size.z));
                this.hasFrame = true;
                this.owner.MarkDirtyRepaint();
            }

        }

        internal void Resume() {

            if (this.registered == false) {
                this.registered = true;
                ParticleAtlasManager.Add(this);
            }

        }

        internal void Suspend() {

            this.playing = false;
            if (this.registered == true) {
                ParticleAtlasManager.Remove(this);
                this.registered = false;
            }

            this.hasFrame = false;
            this.instance.transform.position = this.originalPosition;
            for (var i = 0; i < this.renderers.Length; i++) {
                this.renderers[i].minParticleSize = this.minimumSizes[i];
                this.renderers[i].maxParticleSize = this.maximumSizes[i];
                this.renderers[i].enabled = false;
            }

            if (this.fallback != null) {
                this.fallback.Suspend();
            }

        }

        public void Play() {

            this.Resume();
            this.playing = true;
            this.hasFrame = false;
            if (this.page == null && this.fallback != null) {
                foreach (var renderer in this.renderers) {
                    renderer.enabled = true;
                }

                this.fallback.Play();
            }

            ParticleAtlasManager.Refresh(true);

        }

        public void Stop() {

            this.playing = false;
            this.hasFrame = false;
            foreach (var renderer in this.renderers) {
                renderer.enabled = false;
            }

            if (this.fallback != null) {
                this.fallback.Stop();
            }

            this.owner.MarkDirtyRepaint();
            ParticleAtlasManager.Refresh(true);

        }

        public void Tick() {

            ParticleAtlasManager.Refresh(false);
            if (Application.isPlaying == false && this.page != null) {
                this.page.RenderEditor();
            }
            if (this.page == null && this.fallback != null) {
                this.fallback.PresentTextureFrame();
            }

        }

        public void Draw(MeshGenerationContext context) {

            if (this.page == null || this.hasFrame == false || this.playing == false) {
                return;
            }

            var density = this.owner.effectivePixelsPerUnit;
            var center = this.owner.presentationOrigin;
            var left = center.x + this.completedBounds.min.x * density;
            var top = center.y - this.completedBounds.max.y * density;
            var right = left + this.completedBounds.size.x * density;
            var bottom = top + this.completedBounds.size.y * density;
            var uv = this.page.TileUv(this.slot);
            var mesh = context.Allocate(4, 6, this.page.displayTexture);
            var u0 = uv.xMin;
            var u1 = uv.xMax;
            var v0 = uv.yMin;
            var v1 = uv.yMax;
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(left, top, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(u0, v1), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(right, top, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(u1, v1), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(right, bottom, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(u1, v0), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(left, bottom, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(u0, v0), });
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)2 : (ushort)1);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)1 : (ushort)2);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)3 : (ushort)2);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)2 : (ushort)3);
            mesh.SetNextIndex(0);

        }

        public void Dispose() {

            this.Suspend();
            if (this.fallback != null) {
                this.fallback.Dispose();
                this.fallback = null;
            }


        }

    }

}
