using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private static readonly Dictionary<VisualElement, VisualElement> overlayRoots = new();
        private VisualElement overlayRoot, overlaySurface;
        private IVisualElementScheduledItem overlayTicker;
        private Matrix4x4 overlayMatrix = Matrix4x4.identity;
        internal event Action<MeshGenerationContext> drawOverlayTextures;
        internal event Action<bool> overlayModeChanged;
        internal bool presentationMirrored => this.presentationMatrix.m00 * this.presentationMatrix.m11
            - this.presentationMatrix.m01 * this.presentationMatrix.m10 < 0f;
        public bool renderOnTop { get; private set; }
        internal Matrix4x4 presentationMatrix => this.overlaySurface != null ? this.overlayMatrix : Matrix4x4.identity;

        /// <summary>Draws above content and ancestor clipping on the same panel, retaining source layout and coordinates.</summary>
        public void SetRenderOnTop(bool enabled = true) {

            this.renderOnTop = enabled;
            if (enabled == true) {
                this.UpdateOverlay();
                if (this.overlayTicker == null) {
                    this.overlayTicker = this.schedule.Execute(this.UpdateOverlay).Every(16);
                } else {
                    this.overlayTicker.Resume();
                }
            } else {
                this.RemoveOverlay();
                this.overlayTicker?.Pause();
            }
            this.overlayModeChanged?.Invoke(enabled);
            this.MarkDirtyRepaint();

        }

        private void UpdateOverlay() {

            if (this.renderOnTop == false || this.panel == null) {
                return;
            }
            var root = this.panel.visualTree;
            if (this.overlaySurface == null) {
                if (ParticleElement.overlayRoots.TryGetValue(root, out this.overlayRoot) == false) {
                    this.overlayRoot = ParticleElement.CreateOverlayLayer("particle-overlay-root");
                    ParticleElement.overlayRoots.Add(root, this.overlayRoot);
                    root.Add(this.overlayRoot);
                }
                this.overlaySurface = ParticleElement.CreateOverlayLayer("particle-overlay-effect");
                this.overlaySurface.generateVisualContent += this.DrawOverlay;
                this.overlayRoot.Add(this.overlaySurface);
                this.MarkDirtyRepaint();
            }
            if (root.childCount > 0 && root[root.childCount - 1] != this.overlayRoot) {
                this.overlayRoot.BringToFront();
            }

            var opacity = 1f;
            var visible = this.resolvedStyle.visibility == Visibility.Visible;
            for (var current = (VisualElement)this; current != null && current != root; current = current.parent) {
                opacity *= current.resolvedStyle.opacity;
                visible &= current.resolvedStyle.display != DisplayStyle.None;
            }
            this.overlaySurface.style.opacity = opacity;
            this.overlaySurface.style.visibility = visible == true ? Visibility.Visible : Visibility.Hidden;
            this.overlaySurface.MarkDirtyRepaint();

        }

        private static VisualElement CreateOverlayLayer(string name) {

            var layer = new VisualElement { name = name, pickingMode = PickingMode.Ignore, };
            layer.style.position = Position.Absolute;
            layer.style.left = 0;
            layer.style.top = 0;
            layer.style.right = 0;
            layer.style.bottom = 0;
            layer.style.overflow = Overflow.Visible;
            return layer;

        }

        private void DrawOverlay(MeshGenerationContext context) {

            if (this.backend == null || this.panel == null || this.overlaySurface == null) {
                return;
            }
            this.overlayMatrix = this.overlaySurface.worldTransform.inverse * this.worldTransform;
            this.backend.Draw(context);
            this.drawOverlayTextures?.Invoke(context);

        }

        internal Vector3 PresentationPosition(Vector3 position) {

            if (this.overlaySurface == null) {
                return position;
            }
            var result = this.overlayMatrix.MultiplyPoint3x4(position);
            result.z = Vertex.nearZ;
            return result;

        }

        private void RemoveOverlay() {

            if (this.overlaySurface == null) {
                return;
            }
            var root = this.overlayRoot.parent;
            this.overlaySurface.generateVisualContent -= this.DrawOverlay;
            this.overlaySurface.RemoveFromHierarchy();
            this.overlaySurface = null;
            if (this.overlayRoot.childCount == 0) {
                if (root != null) {
                    ParticleElement.overlayRoots.Remove(root);
                }
                this.overlayRoot.RemoveFromHierarchy();
            }
            this.overlayRoot = null;
            this.overlayMatrix = Matrix4x4.identity;

        }

    }

}
