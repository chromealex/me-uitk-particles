using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles.Demo {

    public sealed class ParticleRevealDemo : IDisposable {

        private readonly CancellationTokenSource cancellation = new();
        private readonly VisualElement container;
        private readonly VisualElement target;
        private ParticleReveal effect;
        private Texture2D snapshot;
        private bool disposed;

        public ParticleRevealDemo(VisualElement parent, PanelSettings settings) {

            this.container = new VisualElement { name = "reveal-demo", };
            this.container.style.marginTop = 16;
            this.container.Add(new Label("VISUAL ELEMENT REVEAL"));
            var stage = new VisualElement();
            stage.style.height = 130;
            stage.style.alignItems = Align.Center;
            stage.style.justifyContent = Justify.Center;
            this.target = CreateContent();
            stage.Add(this.target);
            this.container.Add(stage);
            var controls = new VisualElement();
            controls.style.flexDirection = FlexDirection.Row;
            controls.Add(new Button(() => {
                this.effect?.Dispose();
                this.effect = ParticleReveal.Cloud(this.target, hide: false);
            }) { text = "Cloud reveal", });
            var assemble = new Button(() => {
                this.effect?.Dispose();
                this.effect = ParticleReveal.Assemble(this.target, this.snapshot, hide: false);
            }) { text = "Assemble UI", };
            assemble.SetEnabled(false);
            controls.Add(assemble);
            this.container.Add(controls);
            var hideControls = new VisualElement();
            hideControls.style.flexDirection = FlexDirection.Row;
            hideControls.Add(new Button(() => {
                this.effect?.Dispose();
                this.effect = ParticleReveal.Cloud(this.target, hide: true);
            }) { text = "Hide cloud", });
            hideControls.Add(new Button(() => {
                if (this.snapshot != null) {
                    this.effect?.Dispose();
                    this.effect = ParticleReveal.Assemble(this.target, this.snapshot, hide: true);
                }
            }) { text = "Scatter UI", });
            this.container.Add(hideControls);
            var status = new Label("Preparing UI snapshot...");
            status.style.whiteSpace = WhiteSpace.Normal;
            this.container.Add(status);
            parent.Add(this.container);
            var picker = parent.Q("preset-picker");
            if (picker != null && picker.parent == this.container.parent) {
                this.container.PlaceBehind(picker);
            }
            this.Prepare(settings, assemble, status);

        }

        public static VisualElement CreateContent() {

            var card = new VisualElement();
            card.AddToClassList("reveal-target");
            card.style.width = 240;
            card.style.height = 90;
            card.style.flexShrink = 0;
            card.style.backgroundColor = new Color(.08f, .3f, .38f);
            card.style.borderTopLeftRadius = 14;
            card.style.borderTopRightRadius = 14;
            card.style.borderBottomLeftRadius = 14;
            card.style.borderBottomRightRadius = 14;
            card.style.alignItems = Align.Center;
            card.style.justifyContent = Justify.Center;
            var title = new Label("REWARD UNLOCKED");
            title.style.color = Color.white;
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            card.Add(title);
            var detail = new Label("+ 250 crystals");
            detail.style.color = new Color(.4f, 1f, .85f);
            detail.style.fontSize = 16;
            card.Add(detail);
            return card;

        }

        private async void Prepare(PanelSettings settings, Button button, Label status) {

            try {
                var texture = await ParticleRevealSnapshot.CaptureAsync(CreateContent, settings, new Vector2(240, 90), this.cancellation.Token);
                if (this.disposed == true) {
                    UnityEngine.Object.Destroy(texture);
                    return;
                }

                this.snapshot = texture;
                button.SetEnabled(true);
                status.text = "Cloud burst or assembly from actual UI fragments.";
            } catch (OperationCanceledException) {
                // The demo was closed during capture.
            } catch (Exception exception) {
                status.text = "Snapshot failed: " + exception.Message;
                Debug.LogException(exception);
            }

        }

        public void Dispose() {

            if (this.disposed == true) {
                return;
            }

            this.disposed = true;
            this.cancellation.Cancel();
            this.cancellation.Dispose();
            this.effect?.Dispose();
            if (this.snapshot != null) {
                UnityEngine.Object.Destroy(this.snapshot);
            }

            this.container.RemoveFromHierarchy();

        }

    }

}