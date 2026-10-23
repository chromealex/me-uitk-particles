using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public static class ParticleRevealSnapshot {

        /// <summary>Render a fresh UI subtree offscreen. The caller owns the returned texture.</summary>
        public static async Task<Texture2D> CaptureAsync(Func<VisualElement> createContent, PanelSettings sourceSettings,
            Vector2 size, CancellationToken cancellationToken = default) {

            if (Application.isPlaying == false) {
                throw new InvalidOperationException("Snapshot capture requires Play Mode.");
            }

            if (createContent == null || sourceSettings == null) {
                throw new ArgumentNullException(createContent == null ? nameof(createContent) : nameof(sourceSettings));
            }

            if (float.IsFinite(size.x) == false || float.IsFinite(size.y) == false || size.x <= 0 || size.y <= 0 || size.x > 2048 || size.y > 2048) {
                throw new ArgumentOutOfRangeException(nameof(size), "Snapshot dimensions must be finite and in (0, 2048].");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var content = createContent();
            if (content == null || content.parent != null || content.panel != null) {
                throw new InvalidOperationException("The snapshot factory must return a fresh, detached subtree.");
            }

            PanelSettings settings = null;
            RenderTexture renderTexture = null;
            GameObject host = null;
            Texture2D result = null;
            try {
                var width = Mathf.CeilToInt(size.x);
                var height = Mathf.CeilToInt(size.y);
                renderTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32) {
                    name = "Particle reveal snapshot",
                    hideFlags = HideFlags.HideAndDontSave,
                };
                renderTexture.Create();
                settings = UnityEngine.Object.Instantiate(sourceSettings);
                settings.hideFlags = HideFlags.HideAndDontSave;
                settings.targetTexture = renderTexture;
                settings.renderMode = PanelRenderMode.ScreenSpaceOverlay;
                settings.scaleMode = PanelScaleMode.ConstantPixelSize;
                settings.scale = 1f;
                settings.clearColor = true;
                settings.colorClearValue = Color.clear;
                host = new GameObject("Particle reveal capture") { hideFlags = HideFlags.HideAndDontSave, };
                var document = host.AddComponent<UIDocument>();
                document.panelSettings = settings;
                content.style.width = size.x;
                content.style.height = size.y;
                content.style.flexShrink = 0;
                document.rootVisualElement.Add(content);
                // Allow layout, font atlas updates and an actual panel render before readback.
                for (uint frame = 0u; frame < 3u; frame++) {
                    await Awaitable.EndOfFrameAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                result = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = "Particle reveal snapshot", };
                var previous = RenderTexture.active;
                try {
                    RenderTexture.active = renderTexture;
                    result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    result.Apply();
                } finally {
                    RenderTexture.active = previous;
                }

                // Panel output is premultiplied; UITK texture meshes expect straight alpha.
                var pixels = result.GetPixels();
                for (var i = 0; i < pixels.Length; i++) {
                    var pixel = pixels[i];
                    if (pixel.a > 0f) {
                        pixel.r /= pixel.a;
                        pixel.g /= pixel.a;
                        pixel.b /= pixel.a;
                        pixels[i] = pixel;
                    }
                }

                result.SetPixels(pixels);
                result.Apply();
                return result;
            } catch {
                ParticleEnvironment.Destroy(result);
                throw;
            } finally {
                if (host != null) {
                    host.SetActive(false);
                }

                ParticleEnvironment.Destroy(host);
                ParticleEnvironment.Destroy(settings);
                ParticleEnvironment.Destroy(renderTexture);
            }

        }

    }

}