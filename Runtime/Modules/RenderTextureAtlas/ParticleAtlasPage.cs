using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.UITK.Particles {

    internal sealed class ParticleAtlasPage : System.IDisposable {

        private static readonly HashSet<int> positions = new();
        private readonly int position;
        private readonly ParticleAtlasPresenter[] entries = new ParticleAtlasPresenter[4];
        private readonly Camera camera;
        private readonly RenderTexture rawTexture;
        internal readonly RenderTexture displayTexture;
        private readonly Material resolveMaterial;
        private readonly Vector3 origin;
        internal Vector2 span { get; private set; }
        internal readonly Vector2Int tilePixels;
        private uint count;
        private uint lastEditorFrame = uint.MaxValue;
        internal bool hasSpace => this.count < 4;
        internal bool empty => this.count == 0;

        internal ParticleAtlasPage(Vector2 span, Vector2Int tilePixels) {

            this.span = span;
            this.tilePixels = tilePixels;
            while (ParticleAtlasPage.positions.Contains(this.position) == true) {
                this.position++;
            }

            ParticleAtlasPage.positions.Add(this.position);
            // Separate atlas pages from regular instances, without reserving another scene layer.
            this.origin = new Vector3(-10000 - this.position * 1000, 10000, 0);
            var cameraObject = new GameObject("[Particle UI] Atlas camera") { hideFlags = HideFlags.HideAndDontSave, };
            this.camera = cameraObject.AddComponent<Camera>();
            this.camera.enabled = false;
            this.camera.orthographic = true;
            this.camera.orthographicSize = span.y;
            this.camera.aspect = span.x / span.y;
            this.camera.nearClipPlane = .1f;
            this.camera.farClipPlane = 256;
            this.camera.clearFlags = CameraClearFlags.SolidColor;
            this.camera.backgroundColor = Color.clear;
            this.camera.cullingMask = 1 << ParticleInstance.EFFECT_LAYER;
            cameraObject.transform.position = this.origin + Vector3.back * 128;
            this.rawTexture = new RenderTexture(tilePixels.x * 2, tilePixels.y * 2, 24, RenderTextureFormat.ARGBHalf) {
                name = "Particle atlas raw", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp,
            };
            this.displayTexture = new RenderTexture(tilePixels.x * 2, tilePixels.y * 2, 0, RenderTextureFormat.ARGBHalf) {
                name = "Particle atlas UI", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp,
            };
            this.rawTexture.Create();
            this.displayTexture.Create();
            this.camera.targetTexture = this.rawTexture;
            var shader = Shader.Find("Hidden/ParticleUI/Unpremultiply");
            this.resolveMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, };
            RenderPipelineManager.beginCameraRendering += this.Begin;
            RenderPipelineManager.endCameraRendering += this.End;
            Camera.onPreCull += this.PreCull;
            Camera.onPostRender += this.PostRender;

        }

        internal void SetSpan(Vector2 value) {

            this.span = value;
            this.camera.orthographicSize = value.y;
            this.camera.aspect = value.x / value.y;

        }

        internal void Add(ParticleAtlasPresenter entry) {

            for (var i = 0; i < this.entries.Length; i++) {
                if (this.entries[i] == null) {
                    this.entries[i] = entry;
                    this.count++;
                    entry.UseAtlas(this, i);
                    this.UpdateEnabled();
                    return;
                }
            }

            throw new System.InvalidOperationException("Atlas page is full.");

        }

        internal void Remove(ParticleAtlasPresenter entry) {

            for (var i = 0; i < this.entries.Length; i++) {
                if (this.entries[i] == entry) {
                    this.entries[i] = null;
                    this.count--;
                    break;
                }
            }

            this.UpdateEnabled();

        }

        internal void UpdateEnabled() {

            var active = false;
            foreach (var entry in this.entries) {
                active |= entry != null && entry.playing == true;
            }

            this.camera.enabled = active == true && Application.isPlaying == true;

        }

        internal void RenderEditor() {

            if (this.lastEditorFrame == ParticleEnvironment.editorFrame) {
                return;
            }

            this.lastEditorFrame = ParticleEnvironment.editorFrame;
            this.Begin(default, this.camera);
            ParticleEnvironment.Render(this.camera);
            this.End(default, this.camera);

        }

        internal Vector3 TileCenter(int slot) {

            return this.origin + new Vector3((slot % 2 - .5f) * this.span.x, (slot / 2 - .5f) * this.span.y, 0);

        }

        internal Rect TileUv(int slot) {

            return new Rect(slot % 2 * .5f, slot / 2 * .5f, .5f, .5f);

        }

        private void Begin(ScriptableRenderContext context, Camera renderingCamera) {

            if (renderingCamera != this.camera) {
                return;
            }

            foreach (var entry in this.entries) {
                if (entry != null) {
                    entry.PrepareRender();
                }
            }

        }

        private void End(ScriptableRenderContext context, Camera renderingCamera) {

            if (renderingCamera != this.camera) {
                return;
            }

            // One resolve per page, regardless of how many UI elements display it.
            var previous = RenderTexture.active;
            try {
                Graphics.Blit(this.rawTexture, this.displayTexture, this.resolveMaterial);
            } finally {
                RenderTexture.active = previous;
            }
            foreach (var entry in this.entries) {
                if (entry != null) {
                    entry.CompleteFrame();
                }
            }

        }

        private void PreCull(Camera renderingCamera) {

            if (GraphicsSettings.currentRenderPipeline == null) {
                this.Begin(default, renderingCamera);
            }

        }

        private void PostRender(Camera renderingCamera) {

            if (GraphicsSettings.currentRenderPipeline == null) {
                this.End(default, renderingCamera);
            }

        }

        public void Dispose() {

            RenderPipelineManager.beginCameraRendering -= this.Begin;
            RenderPipelineManager.endCameraRendering -= this.End;
            Camera.onPreCull -= this.PreCull;
            Camera.onPostRender -= this.PostRender;
            this.camera.enabled = false;
            this.camera.targetTexture = null;
            ParticleEnvironment.Destroy(this.camera.gameObject);
            if (RenderTexture.active == this.rawTexture || RenderTexture.active == this.displayTexture) {
                RenderTexture.active = null;
            }

            this.rawTexture.Release();
            this.displayTexture.Release();
            ParticleEnvironment.Destroy(this.rawTexture);
            ParticleEnvironment.Destroy(this.displayTexture);
            ParticleEnvironment.Destroy(this.resolveMaterial);
            ParticleAtlasPage.positions.Remove(this.position);

        }

    }

}
