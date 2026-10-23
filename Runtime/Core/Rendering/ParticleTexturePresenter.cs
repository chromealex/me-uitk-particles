using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Rendering;

namespace ME.UITK.Particles {

    internal sealed class ParticleTexturePresenter : System.IDisposable {

        private readonly ParticleElement owner;
        private readonly GameObject instance;
        private readonly System.Func<Bounds> getBounds;
        private readonly VisualElement textureView;
        private GameObject cameraObject;
        internal Camera effectCamera { get; private set; }
        private RenderTexture rawTexture;
        internal RenderTexture displayTexture;
        private Material resolveMaterial;
        private Bounds cameraBounds, completedBounds;
        private bool hasCompletedFrame, hasPresentedRect, playing;
        private Rect presentedRect;
        public Bounds renderBounds => this.cameraBounds;

        public ParticleTexturePresenter(ParticleElement owner, GameObject instance, System.Func<Bounds> getBounds) {

            this.owner = owner;
            this.instance = instance;
            this.getBounds = getBounds;
            this.textureView = new VisualElement { name = "particle-texture", pickingMode = PickingMode.Ignore, };
            this.textureView.style.position = Position.Absolute;
            this.textureView.style.display = DisplayStyle.None;
            this.owner.Add(this.textureView);
            this.owner.drawOverlayTextures += this.DrawOverlay;
            this.owner.overlayModeChanged += this.SetOverlayMode;
            this.SetOverlayMode(this.owner.renderOnTop);

        }

        private void SetOverlayMode(bool enabled) {

            this.textureView.style.opacity = enabled == true ? 0f : 1f;

        }

        private void DrawOverlay(MeshGenerationContext context) {

            if (this.playing == false || this.hasPresentedRect == false || this.displayTexture == null) {
                return;
            }
            var rect = this.presentedRect;
            var mesh = context.Allocate(4, 6, this.displayTexture);
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(rect.xMin, rect.yMin, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(0, 1), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(rect.xMax, rect.yMin, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(1, 1), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(rect.xMax, rect.yMax, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(1, 0), });
            mesh.SetNextVertex(new Vertex { position = this.owner.PresentationPosition(new Vector3(rect.xMin, rect.yMax, Vertex.nearZ)), tint = this.owner.tint, uv = new Vector2(0, 0), });
            mesh.SetNextIndex(0);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)2 : (ushort)1);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)1 : (ushort)2);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)3 : (ushort)2);
            mesh.SetNextIndex(this.owner.presentationMirrored == true ? (ushort)2 : (ushort)3);
            mesh.SetNextIndex(0);

        }

        public void Initialize() {

            this.cameraObject = new GameObject("[Particle UI] Camera") { hideFlags = HideFlags.HideAndDontSave, };
            this.effectCamera = this.cameraObject.AddComponent<Camera>();
            this.effectCamera.enabled = false;
            this.effectCamera.orthographic = true;
            this.effectCamera.clearFlags = CameraClearFlags.SolidColor;
            this.effectCamera.backgroundColor = Color.clear;
            this.effectCamera.cullingMask = 1 << ParticleInstance.EFFECT_LAYER;
            var shader = Shader.Find("Hidden/ParticleUI/Unpremultiply");
            if (shader != null) {
                this.resolveMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave, };
            }

            this.PrepareTextureFrame();
            RenderPipelineManager.beginCameraRendering += this.OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering += this.OnEndCameraRendering;
            Camera.onPreCull += this.OnCameraPreCull;
            Camera.onPostRender += this.OnCameraPostRender;

        }

        public void Play() {

            this.playing = true;
            this.effectCamera.enabled = Application.isPlaying;

        }

        internal void Suspend() {

            this.playing = false;
            this.effectCamera.enabled = false;
            this.hasCompletedFrame = false;
            this.hasPresentedRect = false;
            this.textureView.style.display = DisplayStyle.None;

        }

        public void Stop() {

            this.playing = false;
            this.hasCompletedFrame = false;
            this.effectCamera.enabled = false;
            if (this.rawTexture != null) {
                ParticleTexturePresenter.ClearTexture(this.rawTexture);
            }

            if (this.displayTexture != null) {
                ParticleTexturePresenter.ClearTexture(this.displayTexture);
            }

        }

        internal void PrepareTextureFrame() {

            this.cameraBounds = this.getBounds();
            var density = Mathf.Max(1f, this.owner.effectivePixelsPerUnit);
            var requestedWidth = Mathf.Max(1f, this.cameraBounds.size.x * density);
            var requestedHeight = Mathf.Max(1f, this.cameraBounds.size.y * density);
            var limit = Mathf.Min(1024, SystemInfo.maxTextureSize);
            var resolutionScale = Mathf.Min(1f, limit / Mathf.Max(requestedWidth, requestedHeight));
            var width = Mathf.Min(Mathf.CeilToInt(requestedWidth * resolutionScale / 64f) * 64, limit);
            var height = Mathf.Min(Mathf.CeilToInt(requestedHeight * resolutionScale / 64f) * 64, limit);
            if (this.rawTexture == null || this.rawTexture.IsCreated() == false || this.rawTexture.width < width || this.rawTexture.height < height) {
                if (this.rawTexture != null) {
                    width = Mathf.Max(width, this.rawTexture.width);
                    height = Mathf.Max(height, this.rawTexture.height);
                }

                this.effectCamera.targetTexture = null;
                ParticleTexturePresenter.DestroyTexture(ref this.rawTexture);
                this.rawTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGBHalf) {
                    name = "Particle UI camera",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                this.rawTexture.Create();
                ParticleTexturePresenter.ClearTexture(this.rawTexture);
                this.effectCamera.targetTexture = this.rawTexture;
            }

            var center = this.cameraBounds.center;
            this.cameraObject.transform.position = this.instance.transform.position + new Vector3(center.x, center.y, this.cameraBounds.min.z - 1f);
            this.effectCamera.nearClipPlane = .1f;
            this.effectCamera.farClipPlane = Mathf.Max(2f, this.cameraBounds.size.z + 2f);
            this.effectCamera.orthographicSize = Mathf.Max(.05f, this.cameraBounds.size.y * .5f);
            this.effectCamera.aspect = this.cameraBounds.size.x / this.cameraBounds.size.y;

        }

        private void OnBeginCameraRendering(ScriptableRenderContext context, Camera camera) {

            if (camera == this.effectCamera && this.instance != null) {
                // URP invokes this before constructing camera data and performing culling.
                this.PrepareTextureFrame();
            }

        }

        internal void OnEndCameraRendering(ScriptableRenderContext context, Camera camera) {

            if (camera == this.effectCamera) {
                this.completedBounds = this.cameraBounds;
                this.hasCompletedFrame = true;
            }

        }

        private void OnCameraPreCull(Camera camera) {

            if (GraphicsSettings.currentRenderPipeline == null) {
                this.OnBeginCameraRendering(default, camera);
            }

        }

        private void OnCameraPostRender(Camera camera) {

            if (GraphicsSettings.currentRenderPipeline == null) {
                this.OnEndCameraRendering(default, camera);
            }

        }

        internal void PresentTextureFrame() {

            this.textureView.style.unityBackgroundImageTintColor = this.owner.tint;

            if (Application.isPlaying == false && this.playing == true) {
                this.PrepareTextureFrame();
                ParticleEnvironment.Render(this.effectCamera);
                this.OnEndCameraRendering(default, this.effectCamera);
            }
            if (this.hasCompletedFrame == false || this.rawTexture == null) {
                return;
            }

            if (this.displayTexture == null || this.displayTexture.IsCreated() == false || this.displayTexture.width != this.rawTexture.width || this.displayTexture.height != this.rawTexture.height) {
                this.textureView.style.backgroundImage = StyleKeyword.None;
                ParticleTexturePresenter.DestroyTexture(ref this.displayTexture);
                this.displayTexture = new RenderTexture(this.rawTexture.width, this.rawTexture.height, 0, RenderTextureFormat.ARGBHalf) {
                    name = "Particle UI straight alpha",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                this.displayTexture.Create();
                this.textureView.style.backgroundImage = Background.FromRenderTexture(this.displayTexture);
            }

            var previous = RenderTexture.active;
            try {
                if (this.resolveMaterial != null) {
                    Graphics.Blit(this.rawTexture, this.displayTexture, this.resolveMaterial);
                } else {
                    Graphics.Blit(this.rawTexture, this.displayTexture);
                }
            } finally {
                RenderTexture.active = previous;
            }

            this.hasCompletedFrame = false;
            // Map the completed camera frame, not the next frame's bounds. Keep the emitter fixed
            // at contentRect.center even when the bounds are asymmetric or grow during playback.
            var density = this.owner.effectivePixelsPerUnit;
            var rect = new Rect(this.owner.presentationOrigin.x + this.completedBounds.min.x * density,
                this.owner.presentationOrigin.y - this.completedBounds.max.y * density,
                this.completedBounds.size.x * density, this.completedBounds.size.y * density);
            if (this.hasPresentedRect == false || this.presentedRect != rect) {
                this.textureView.style.left = rect.x;
                this.textureView.style.top = rect.y;
                this.textureView.style.width = rect.width;
                this.textureView.style.height = rect.height;
                this.textureView.style.display = DisplayStyle.Flex;
                this.presentedRect = rect;
                this.hasPresentedRect = true;
            }

            this.textureView.MarkDirtyRepaint();

        }

        private static void ClearTexture(RenderTexture texture) {

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = previous;

        }

        private static void DestroyTexture(ref RenderTexture texture) {

            if (texture == null) {
                return;
            }

            if (RenderTexture.active == texture) {
                RenderTexture.active = null;
            }

            texture.Release();
            ParticleEnvironment.Destroy(texture);
            texture = null;

        }

        public void Dispose() {

            this.owner.drawOverlayTextures -= this.DrawOverlay;
            this.owner.overlayModeChanged -= this.SetOverlayMode;

            RenderPipelineManager.beginCameraRendering -= this.OnBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= this.OnEndCameraRendering;
            Camera.onPreCull -= this.OnCameraPreCull;
            Camera.onPostRender -= this.OnCameraPostRender;
            this.textureView.style.backgroundImage = StyleKeyword.None;
            this.textureView.RemoveFromHierarchy();
            if (this.effectCamera != null) {
                this.effectCamera.enabled = false;
                this.effectCamera.targetTexture = null;
            }

            if (this.cameraObject != null) {
                ParticleEnvironment.Destroy(this.cameraObject);
            }

            if (this.resolveMaterial != null) {
                ParticleEnvironment.Destroy(this.resolveMaterial);
            }

            ParticleTexturePresenter.DestroyTexture(ref this.rawTexture);
            ParticleTexturePresenter.DestroyTexture(ref this.displayTexture);

        }

    }

}
