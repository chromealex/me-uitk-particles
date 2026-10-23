using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        public float emissionRate { get; private set; }
        public VisualElement emissionArea { get; private set; }
        public int emissionSystemIndex { get; private set; }
        private string declarativeEmissionArea;
        private double emissionRemainder;
        private bool managedEmissionStopped;
        private bool hasManagedEmission => this.backend != null && this.supportsParticleAccess == true && this.requestedPlay == true
            && this.emissionRate > 0f && this.managedEmissionStopped == false;

        /// <summary>Sets an additive managed emission rate, in particles per scaled second. Does not start playback.</summary>
        public void SetEmissionRate(float particlesPerSecond, int systemIndex = 0) {

            this.RequireCpuControl();
            if (float.IsFinite(particlesPerSecond) == false || particlesPerSecond < 0f) {
                throw new ArgumentOutOfRangeException(nameof(particlesPerSecond));
            }
            if (systemIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(systemIndex));
            }
            if (this.backend != null) {
                this.GetParticleCount(systemIndex);
            }

            this.emissionRate = particlesPerSecond;
            this.emissionSystemIndex = systemIndex;
            this.emissionRemainder = 0d;

        }

        /// <summary>Binds the managed stream to the target's current rectangular border box. Requires valid attached layout.</summary>
        public void BindEmissionArea(VisualElement target, int systemIndex = 0) {

            this.RequireCpuControl();
            this.EnsureInstance();
            this.GetParticleCount(systemIndex);
            this.ValidateTarget(target, systemIndex);
            this.declarativeEmissionArea = null;
            this.emissionArea = target;
            this.emissionSystemIndex = systemIndex;
            this.emissionRemainder = 0d;

        }

        /// <summary>Returns the managed stream to the selected system's Shape without stopping the rate.</summary>
        public void UnbindEmissionArea() {

            this.declarativeEmissionArea = null;
            this.emissionArea = null;
            this.emissionRemainder = 0d;

        }

        private void ResolveEmissionArea() {

            if (string.IsNullOrEmpty(this.declarativeEmissionArea) == true) {
                return;
            }

            if (this.emissionArea != null && this.emissionArea.panel == this.panel && this.emissionArea.name == this.declarativeEmissionArea) {
                return;
            }

            var target = this.panel?.visualTree.Q(this.declarativeEmissionArea);
            if (this.emissionArea != target) {
                this.emissionArea = target;
                this.emissionRemainder = 0d;
            }

        }

        private void UpdateManagedEmission(float deltaTime) {

            if (this.hasManagedEmission == false || this.isPaused == true || deltaTime <= 0f || this.simulationSpeed == 0f) {
                return;
            }
            if ((uint)this.emissionSystemIndex >= this.particleSystemCount) {
                return;
            }

            this.ResolveEmissionArea();
            if (string.IsNullOrEmpty(this.declarativeEmissionArea) == false && this.emissionArea == null) {
                this.emissionRemainder = 0d;
                return;
            }

            var target = this.emissionArea;
            if (target != null) {
                if (target.panel != this.panel || target.layout.width <= 0f || target.layout.height <= 0f) {
                    this.emissionRemainder = 0d;
                    return;
                }
                try {
                    this.ValidateTarget(target, this.emissionSystemIndex);
                } catch (InvalidOperationException) {
                    this.emissionRemainder = 0d;
                    return;
                }
            }

            this.emissionRemainder += (double)this.emissionRate * deltaTime * this.simulationSpeed;
            var whole = Math.Floor(this.emissionRemainder);
            this.emissionRemainder -= whole;
            var count = (uint)Math.Min(whole, int.MaxValue);
            if (count == 0) {
                return;
            }

            if (target == null) {
                this.Emit(count, this.emissionSystemIndex);
            } else {
                this.EmitIn(target, count, this.emissionSystemIndex);
            }

        }

    }

}
