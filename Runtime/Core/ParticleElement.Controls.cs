using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private float configuredSimulationSpeed = 1f;
        private readonly List<Vector3> emissionPositions = new();
        private ParticleRandom emissionRandom = new(System.Environment.TickCount);
        public Vector2 positionOffset { get; private set; }
        public Color tint { get; private set; } = Color.white;
        internal Vector2 presentationOrigin => this.contentRect.center + this.positionOffset;
        public bool isAlive => this.backend != null && (this.backend.isAlive == true || this.hasManagedEmission == true);
        public bool isPlaying => this.requestedPlay == true && this.isPaused == false && this.isAlive == true;

        /// <summary>Nonnegative multiplier of each prefab system's simulation speed. Zero freezes simulation, not completion time.</summary>
        public float simulationSpeed {
            get => this.configuredSimulationSpeed;
            set {
                if (float.IsFinite(value) == false || value < 0f) {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }

                this.backend?.SetSimulationSpeed(value);
                this.configuredSimulationSpeed = value;
            }
        }

        /// <summary>Capabilities of the initialized backend. VFXGraph StopEmitting requires graph handling of OnStop.</summary>
        public ParticleCapabilities capabilities {
            get {
                if (this.backend == null) {
                    return ParticleCapabilities.None;
                }

                var result = ParticleCapabilities.Pause | ParticleCapabilities.StopEmitting | ParticleCapabilities.SimulationSpeed
                    | ParticleCapabilities.Position | ParticleCapabilities.Tint | ParticleCapabilities.Scale
                    | ParticleCapabilities.AsyncPlayback | ParticleCapabilities.Prewarm;
                if (this.supportsParticleAccess == true) {
                    result |= ParticleCapabilities.ParticleAccess | ParticleCapabilities.Emit | ParticleCapabilities.EmitIn | ParticleCapabilities.CoordinateConversion
                        | ParticleCapabilities.EmitAt | ParticleCapabilities.EmitAlongBorder | ParticleCapabilities.Attractor
                        | ParticleCapabilities.ManualSimulation | ParticleCapabilities.RandomSeed
                        | ParticleCapabilities.EmitParameters | ParticleCapabilities.EmitTowards | ParticleCapabilities.ArrivalEvents
                        | ParticleCapabilities.EmissionRate | ParticleCapabilities.EmissionArea;
                }

                if (this.supportsStateCapture == true) {
                    result |= ParticleCapabilities.StateCapture;
                }

                return result;
            }
        }

        /// <summary>Sets a UI-unit offset from contentRect.center, without moving the element or changing simulation coordinates.</summary>
        public void SetPosition(Vector2 offset) {

            if (float.IsFinite(offset.x) == false || float.IsFinite(offset.y) == false) {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            this.positionOffset = offset;
            this.MarkDirtyRepaint();

        }

        /// <summary>Multiplies final presentation color and alpha. Channels must be finite and in [0, 1].</summary>
        public void SetTint(Color color) {

            for (var i = 0; i < 4; i++) {
                if (float.IsFinite(color[i]) == false || color[i] < 0f || color[i] > 1f) {
                    throw new ArgumentOutOfRangeException(nameof(color));
                }
            }

            this.tint = color;
            this.MarkDirtyRepaint();

        }

        /// <summary>
        /// Adds particles to one system without clearing existing particles. Starts a manual run when stopped,
        /// suppressing the prefab's automatic emission for that run. Counts are capped to the available capacity.
        /// GPU reuses its sampled start templates. VFXGraph is unsupported. A paused run stays paused.
        /// </summary>
        public void Emit(uint count, int systemIndex = 0) {

            var accepted = this.ValidateEmission(count, systemIndex);
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            this.EmitParticles(accepted, systemIndex, null);

        }

        /// <summary>
        /// Emits uniformly over the target's rectangular border box, transformed into this element's coordinates.
        /// Target and effect must share a panel and have resolved, nonzero layout. Rounded corners and clipping are not sampled.
        /// The region is sampled on each call; subsequent layout changes do not move already emitted particles.
        /// </summary>
        public void EmitIn(VisualElement target, uint count, int systemIndex = 0) {

            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            var accepted = this.ValidateEmission(count, systemIndex);
            if (this.panel == null || target.panel != this.panel) {
                throw new InvalidOperationException("The target and ParticleElement must be attached to the same panel.");
            }

            var size = target.layout.size;
            if (float.IsFinite(size.x) == false || float.IsFinite(size.y) == false || size.x <= 0f || size.y <= 0f) {
                throw new InvalidOperationException("EmitIn requires a target with resolved, nonzero layout. Wait for GeometryChangedEvent.");
            }

            var origin = target.ChangeCoordinatesTo(this, Vector2.zero);
            var x = target.ChangeCoordinatesTo(this, new Vector2(size.x, 0)) - origin;
            var y = target.ChangeCoordinatesTo(this, new Vector2(0, size.y)) - origin;
            if (float.IsFinite(origin.x + origin.y + x.x + x.y + y.x + y.y) == false
                || Mathf.Abs(x.x * y.y - x.y * y.x) < 1e-8f) {
                throw new InvalidOperationException("EmitIn requires a finite, non-degenerate target transform.");
            }

            // Validate before activating a stopped effect; activation may reposition an atlas instance.
            this.LocalToSimulation(origin, systemIndex);
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            var inverse = this.SimulationToLocalMatrix(systemIndex).inverse;
            this.emissionPositions.Clear();
            for (var i = 0; i < accepted; i++) {
                var local = origin + x * (float)this.emissionRandom.NextDouble() + y * (float)this.emissionRandom.NextDouble();
                this.emissionPositions.Add(inverse.MultiplyPoint3x4(local));
            }

            this.EmitParticles(accepted, systemIndex, this.emissionPositions);

        }

        private int ValidateEmission(uint count, int systemIndex) {

            if (count > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            this.EnsureInstance();
            if (this.backend is GpuParticleBackend shader) {
                this.ValidateGpuSystem(systemIndex);
                return Mathf.Min((int)count, shader.GetSystem(systemIndex).availableEmissionCount);
            }

            var system = this.GetNativeSystem(systemIndex);
            return Mathf.Min((int)count, Mathf.Max(0, system.main.maxParticles - system.particleCount));

        }

        private void PrepareManualEmission() {

            if (this.requestedPlay == false) {
                this.Play();
                this.backend.StopEmitting();
                if (this.backend is NativeParticleBackend native) {
                    foreach (var system in native.systems) {
                        if (system != null) {
                            system.Clear(false);
                        }
                    }
                } else if (this.backend is GpuParticleBackend shader) {
                    shader.ClearSystems();
                }
            }

            if (this.hasCompletedPlayback == true) {
                this.hasCompletedPlayback = false;
                this.playbackCompletion = this.configuredCompletion;
                this.playbackStartTime = ParticleEnvironment.Now(this.playbackCompletion.clock == ParticleCompletionClock.Unscaled);
                this.pauseStartTime = this.playbackStartTime;
                this.manualElapsed = 0;
                this.playbackStartFrame = Time.frameCount;
                this.observedParticles = false;
                this.completionPending = true;
            }

        }

        private void EmitParticles(int count, int systemIndex, List<Vector3> positions, ParticleEmitParams overrides = default, List<uint> seeds = null) {

            if (this.backend is GpuParticleBackend shader) {
                shader.GetSystem(systemIndex).Emit(count, positions, overrides, seeds);
            } else {
                ((NativeParticleBackend)this.backend).Emit(count, systemIndex, positions, overrides, seeds);
            }

            if (this.isPaused == true || this.automaticSimulation == false) {
                this.backend.Pause();
            }

            this.MarkDirtyRepaint();

        }

    }

}
