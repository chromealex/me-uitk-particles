using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private IVisualElementScheduledItem presentationTicker;
        private bool presentationTransition;
        private float startDensity, targetDensity;
        private Bounds startBounds, targetBounds;
        private float startVisualScale, targetVisualScale, startSpeed, targetSpeed;
        private Vector3 startSystemScale, targetSystemScale;
        private Vector2 startPosition, targetPosition;
        private Color startTint, targetTint;
        private double transitionStart;
        private float transitionDuration;
        private string transitionEasing;

        private float ReadTransitionTime(string name) {

            var text = this.ReadParticleStyle(name, "0");
            var milliseconds = text.EndsWith("ms", StringComparison.Ordinal);
            var seconds = text.EndsWith("s", StringComparison.Ordinal);
            var number = milliseconds == true ? text.Substring(0, text.Length - 2)
                : seconds == true ? text.Substring(0, text.Length - 1) : text;
            var value = float.Parse(number, CultureInfo.InvariantCulture) * (milliseconds == true ? .001f : 1f);
            if (float.IsNaN(value) == true || float.IsInfinity(value) == true || value < 0f) {
                throw new ArgumentOutOfRangeException(name, "Transition times must be finite and nonnegative.");
            }

            return value;

        }

        private void SetPresentationTarget(float density, Bounds bounds, float duration, float delay, string easing, bool replacing,
            float visualScale, Vector3 systemScale, Vector2 position, Color tint, float speed) {

            // Unrelated style resolutions must not restart an in-flight transition.
            if (this.presentationTransition == true && this.targetDensity == density && this.targetBounds == bounds
                && this.targetVisualScale == visualScale && this.targetSystemScale == systemScale
                && this.targetPosition == position && this.targetTint == tint && this.targetSpeed == speed
                && duration > 0f && replacing == false) {
                return;
            }

            if (this.presentationTransition == true) {
                this.UpdatePresentationTransition();
            }

            this.presentationTicker?.Pause();
            this.presentationTransition = false;
            this.targetDensity = density;
            this.targetBounds = bounds;
            this.targetVisualScale = visualScale;
            this.targetSystemScale = systemScale;
            this.targetPosition = position;
            this.targetTint = tint;
            this.targetSpeed = speed;
            if (this.markupApplied == false || this.panel == null || replacing == true || duration == 0f) {
                this.pixelsPerUnit = density;
                this.gpuBounds = bounds;
                this.ApplyPresentationControls(visualScale, systemScale, position, tint, speed);
                return;
            }

            if (this.pixelsPerUnit == density && this.gpuBounds == bounds
                && this.visualScale == visualScale && this.systemScale == systemScale && this.positionOffset == position
                && this.tint == tint && this.simulationSpeed == speed) {
                return;
            }

            this.startDensity = this.pixelsPerUnit;
            this.startBounds = this.gpuBounds;
            this.startVisualScale = this.visualScale;
            this.startSystemScale = this.systemScale;
            this.startPosition = this.positionOffset;
            this.startTint = this.tint;
            this.startSpeed = this.simulationSpeed;
            this.transitionStart = ParticleEnvironment.Now(true) + delay;
            this.transitionDuration = duration;
            this.transitionEasing = easing;
            this.presentationTransition = true;
            if (this.presentationTicker == null) {
                this.presentationTicker = this.schedule.Execute(this.UpdatePresentationTransition).Every(16);
            } else {
                this.presentationTicker.Resume();
            }

        }

        private void UpdatePresentationTransition() {

            if (this.presentationTransition == false) {
                return;
            }

            var progress = Mathf.Clamp01((float)((ParticleEnvironment.Now(true) - this.transitionStart) / this.transitionDuration));
            var eased = this.transitionEasing switch {
                "ease-in" => progress * progress,
                "ease-out" => 1f - (1f - progress) * (1f - progress),
                "ease-in-out" => progress * progress * (3f - 2f * progress),
                _ => progress,
            };
            this.pixelsPerUnit = Mathf.Lerp(this.startDensity, this.targetDensity, eased);
            this.gpuBounds = new Bounds(Vector3.Lerp(this.startBounds.center, this.targetBounds.center, eased),
                Vector3.Lerp(this.startBounds.size, this.targetBounds.size, eased));
            this.ApplyPresentationControls(Mathf.Lerp(this.startVisualScale, this.targetVisualScale, eased),
                Vector3.Lerp(this.startSystemScale, this.targetSystemScale, eased), Vector2.Lerp(this.startPosition, this.targetPosition, eased),
                Color.Lerp(this.startTint, this.targetTint, eased), Mathf.Lerp(this.startSpeed, this.targetSpeed, eased));
            this.MarkDirtyRepaint();
            if (progress >= 1f) {
                this.FinishPresentationTransition();
            }

        }

        private void ApplyPresentationControls(float visualScale, Vector3 systemScale, Vector2 position, Color tint, float speed) {

            this.SetVisualScale(visualScale);
            this.SetSystemScale(systemScale);
            this.SetPosition(position);
            this.SetTint(tint);
            this.simulationSpeed = speed;

        }

        private void FinishPresentationTransition() {

            if (this.presentationTransition == true) {
                this.pixelsPerUnit = this.targetDensity;
                this.gpuBounds = this.targetBounds;
                this.ApplyPresentationControls(this.targetVisualScale, this.targetSystemScale, this.targetPosition, this.targetTint, this.targetSpeed);
            }

            this.presentationTransition = false;
            this.presentationTicker?.Pause();

        }

    }

}
