using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private bool automaticSimulationEnabled = true;
        private double manualElapsed, lastAttractorTime;
        private TaskCompletionSource<bool> asyncPlayback;
        private VisualElement attractorTarget;
        private string declarativeAttractor;
        private float attractorStrength, attractorRadius;
        private bool attractorKills;
        private int attractorSystemIndex;
        private ParticleSystem.Particle[] attractionBuffer = Array.Empty<ParticleSystem.Particle>();
        private readonly List<Vector2> borderPoints = new();

        /// <summary>Explicit seed used on the next Play; null retains the prefab's seed policy.</summary>
        public uint? randomSeed { get; private set; }

        /// <summary>Disable automatic stepping before calling Simulate. VFXGraph only supports automatic stepping.</summary>
        public bool automaticSimulation {
            get => this.automaticSimulationEnabled;
            set {
                if (this.automaticSimulationEnabled == value) {
                    return;
                }
                if (value == false) {
                    this.RequireCpuControl();
                }

                var now = ParticleEnvironment.Now(this.playbackCompletion.clock == ParticleCompletionClock.Unscaled);
                if (this.requestedPlay == true && this.backend != null) {
                    if (value == false) {
                        this.manualElapsed = (this.isPaused == true ? this.pauseStartTime : now) - this.playbackStartTime;
                        if (this.backend is NativeParticleBackend && this.isPaused == false) {
                            this.backend.Pause();
                        }
                    } else {
                        this.playbackStartTime = now - this.manualElapsed;
                        this.pauseStartTime = now;
                        if (this.isPaused == false) {
                            this.backend.Resume();
                        }
                    }
                }

                this.lastAttractorTime = ParticleEnvironment.Now(false);
                this.lastEditorTime = ParticleEnvironment.Now(true);
                this.automaticSimulationEnabled = value;
                this.ResetFlightClocks();
            }
        }

        /// <summary>Sets a nonzero deterministic seed for the next run, without changing current particles.</summary>
        public void SetRandomSeed(uint seed) {

            this.RequireCpuControl();
            if (seed == 0) {
                throw new ArgumentOutOfRangeException(nameof(seed), "Use a nonzero seed.");
            }

            this.randomSeed = seed;

        }

        /// <summary>Creates backend resources without starting or advancing playback. Requires an attached panel and prefab.</summary>
        public void Prewarm() {

            this.EnsureInstance();
            if (this.backend == null) {
                throw new InvalidOperationException("Prewarm requires an attached panel and an assigned prefab.");
            }

            if (this.requestedPlay == false) {
                this.backend.Tick();
            }
            this.MarkDirtyRepaint();

        }

        /// <summary>Steps an active, unpaused run in manual mode. Completion time advances by the supplied delta.</summary>
        public void Simulate(float deltaTime) {

            this.RequireCpuControl();
            if (float.IsFinite(deltaTime) == false || deltaTime < 0f) {
                throw new ArgumentOutOfRangeException(nameof(deltaTime));
            }
            if (this.automaticSimulation == true || this.backend == null || this.requestedPlay == false) {
                throw new InvalidOperationException("Simulate requires an initialized run and automaticSimulation = false.");
            }
            if (this.isPaused == true || deltaTime == 0f) {
                return;
            }

            this.ProtectFlightsForStep(deltaTime);
            if (this.backend is NativeParticleBackend native) {
                native.SimulateEditor(deltaTime);
            } else if (this.backend is GpuParticleBackend shader) {
                shader.Simulate(deltaTime);
            }

            this.backend.Tick();
            var version = this.flightVersion;
            this.UpdateFlights(deltaTime, true);
            if (version != this.flightVersion) {
                return;
            }
            this.UpdateAttractor(deltaTime);
            if (version != this.flightVersion) {
                return;
            }
            this.UpdateManagedEmission(deltaTime);
            this.manualElapsed += deltaTime;
            this.CheckCompletion(this.manualElapsed);
            this.MarkDirtyRepaint();

        }

        /// <summary>
        /// Restarts playback and waits for completion. Stop, replacement, detach, Dispose or another Play cancel the task.
        /// Token cancellation cancels only the wait. Call on the UI thread with an attached panel and prefab.
        /// </summary>
        public async Task PlayAsync(CancellationToken cancellationToken = default) {

            cancellationToken.ThrowIfCancellationRequested();
            this.Prewarm();
            this.Play();
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            this.asyncPlayback = completion;
            using (cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken))) {
                try {
                    await completion.Task;
                } finally {
                    if (this.asyncPlayback == completion) {
                        this.asyncPlayback = null;
                    }
                }
            }

        }

        /// <summary>Emits at a normalized point in the target border box: (0,0) top-left, (1,1) bottom-right.</summary>
        public void EmitAt(VisualElement target, Vector2 normalizedPosition, uint count, int systemIndex = 0) {

            if (float.IsFinite(normalizedPosition.x) == false || float.IsFinite(normalizedPosition.y) == false
                || normalizedPosition.x < 0f || normalizedPosition.x > 1f || normalizedPosition.y < 0f || normalizedPosition.y > 1f) {
                throw new ArgumentOutOfRangeException(nameof(normalizedPosition));
            }

            var accepted = this.ValidateEmission(count, systemIndex);
            var size = this.ValidateTarget(target, systemIndex);
            var local = target.ChangeCoordinatesTo(this, Vector2.Scale(size, normalizedPosition));
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            var position = this.LocalToSimulation(local, systemIndex);
            this.emissionPositions.Clear();
            for (var i = 0; i < accepted; i++) {
                this.emissionPositions.Add(position);
            }

            this.EmitParticles(accepted, systemIndex, this.emissionPositions);

        }

        /// <summary>
        /// Emits along the transformed border perimeter. Inset is in target-local UI units.
        /// Rounded corners use resolved USS radii, approximated by sixteen segments per quarter circle.
        /// </summary>
        public void EmitAlongBorder(VisualElement target, uint count, float inset = 0f, bool roundedCorners = true, int systemIndex = 0) {

            if (float.IsFinite(inset) == false || inset < 0f) {
                throw new ArgumentOutOfRangeException(nameof(inset));
            }

            var accepted = this.ValidateEmission(count, systemIndex);
            var size = this.ValidateTarget(target, systemIndex);
            if (inset * 2f >= Mathf.Min(size.x, size.y)) {
                throw new ArgumentOutOfRangeException(nameof(inset), "Inset must leave a positive rectangle.");
            }

            this.BuildBorder(target, size, inset, roundedCorners);
            var length = 0f;
            for (var i = 0; i < this.borderPoints.Count; i++) {
                length += Vector2.Distance(this.borderPoints[i], this.borderPoints[(i + 1) % this.borderPoints.Count]);
            }
            if (float.IsFinite(length) == false || length <= 0f) {
                throw new InvalidOperationException("The target perimeter is degenerate.");
            }
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            var inverse = this.SimulationToLocalMatrix(systemIndex).inverse;
            this.emissionPositions.Clear();
            for (var i = 0; i < accepted; i++) {
                var distance = (float)this.emissionRandom.NextDouble() * length;
                for (var j = 0; j < this.borderPoints.Count; j++) {
                    var from = this.borderPoints[j];
                    var to = this.borderPoints[(j + 1) % this.borderPoints.Count];
                    var segment = Vector2.Distance(from, to);
                    if (distance <= segment || j == this.borderPoints.Count - 1) {
                        var point = Vector2.Lerp(from, to, segment > 0f ? distance / segment : 0f);
                        this.emissionPositions.Add(inverse.MultiplyPoint3x4(point));
                        break;
                    }

                    distance -= segment;
                }
            }

            this.EmitParticles(accepted, systemIndex, this.emissionPositions);

        }

        /// <summary>
        /// Accelerates one system toward the target center in the UI plane. Strength is UI units/second squared.
        /// Optional capture radius is in this element's UI units; killOnArrival removes particles inside it.
        /// Detached/unresolved targets suspend attraction until their geometry is valid again.
        /// </summary>
        public void SetAttractor(VisualElement target, float strength, float captureRadius = 0f, bool killOnArrival = false, int systemIndex = 0) {

            this.RequireCpuControl();
            if (float.IsFinite(strength) == false || strength < 0f || float.IsFinite(captureRadius) == false || captureRadius < 0f) {
                throw new ArgumentOutOfRangeException(nameof(strength), "Strength and capture radius must be finite and nonnegative.");
            }

            this.ValidateTarget(target, systemIndex);
            this.GetParticleCount(systemIndex);
            this.declarativeAttractor = null;
            this.attractorTarget = target;
            this.attractorStrength = strength;
            this.attractorRadius = captureRadius;
            this.attractorKills = killOnArrival;
            this.attractorSystemIndex = systemIndex;
            this.lastAttractorTime = ParticleEnvironment.Now(false);

        }

        public void ClearAttractor() {

            this.declarativeAttractor = null;
            this.attractorTarget = null;

        }

        private Vector2 ValidateTarget(VisualElement target, int systemIndex) {

            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }
            if (this.panel == null || target.panel != this.panel) {
                throw new InvalidOperationException("Target and particles must share an attached panel.");
            }

            var size = target.layout.size;
            var origin = target.ChangeCoordinatesTo(this, Vector2.zero);
            var x = target.ChangeCoordinatesTo(this, new Vector2(size.x, 0)) - origin;
            var y = target.ChangeCoordinatesTo(this, new Vector2(0, size.y)) - origin;
            if (float.IsFinite(size.x + size.y + origin.x + origin.y + x.x + x.y + y.x + y.y) == false
                || size.x <= 0f || size.y <= 0f || Mathf.Abs(x.x * y.y - x.y * y.x) < 1e-8f) {
                throw new InvalidOperationException("Target requires resolved, finite, non-degenerate layout.");
            }

            this.LocalToSimulation(origin, systemIndex);
            return size;

        }

        private void BuildBorder(VisualElement target, Vector2 size, float inset, bool rounded) {

            var style = target.resolvedStyle;
            var radii = new Vector4(style.borderTopLeftRadius, style.borderTopRightRadius, style.borderBottomRightRadius, style.borderBottomLeftRadius);
            if (rounded == false) {
                radii = Vector4.zero;
            }

            // Normalize adjacent radii together, as in CSS, before applying the inward offset.
            var factor = Mathf.Min(1f, size.x / Mathf.Max(.0001f, radii.x + radii.y), size.x / Mathf.Max(.0001f, radii.z + radii.w),
                size.y / Mathf.Max(.0001f, radii.x + radii.w), size.y / Mathf.Max(.0001f, radii.y + radii.z));
            radii *= factor;
            this.borderPoints.Clear();
            for (var corner = 0; corner < 4; corner++) {
                var radius = Mathf.Max(0, radii[corner] - inset);
                var right = corner == 1 || corner == 2;
                var bottom = corner >= 2;
                var center = new Vector2(right == true ? size.x - inset - radius : inset + radius,
                    bottom == true ? size.y - inset - radius : inset + radius);
                for (var step = 0; step <= 16; step++) {
                    var angle = (180f + corner * 90f + step * (90f / 16f)) * Mathf.Deg2Rad;
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    this.borderPoints.Add(target.ChangeCoordinatesTo(this, point));
                }
            }

        }

        private void UpdateAttractor(float deltaTime) {

            if (string.IsNullOrEmpty(this.declarativeAttractor) == false
                && (this.attractorTarget == null || this.attractorTarget.panel != this.panel || this.attractorTarget.name != this.declarativeAttractor)) {
                this.attractorTarget = this.panel?.visualTree.Q(this.declarativeAttractor);
            }

            var target = this.attractorTarget;
            if (target == null || target.panel != this.panel || this.panel == null || this.supportsParticleAccess == false
                || (uint)this.attractorSystemIndex >= this.particleSystemCount || deltaTime <= 0f || this.simulationSpeed == 0f || this.isPaused == true) {
                return;
            }

            var size = target.layout.size;
            var destination = target.ChangeCoordinatesTo(this, size * .5f);
            if (size.x <= 0f || size.y <= 0f || float.IsFinite(destination.x + destination.y) == false) {
                return;
            }

            var index = this.attractorSystemIndex;
            var count = checked((int)this.GetParticleCount(index));
            if (this.attractionBuffer.Length < count) {
                Array.Resize(ref this.attractionBuffer, count);
            }

            count = this.GetParticles(this.attractionBuffer, systemIndex: index);
            var forward = this.SimulationToLocalMatrix(index);
            if (float.IsFinite(forward.determinant) == false || Mathf.Abs(forward.determinant) < 1e-12f) {
                return;
            }

            var inverse = forward.inverse;
            var captured = 0u;
            for (var i = 0; i < count; i++) {
                var particle = this.attractionBuffer[i];
                var local = forward.MultiplyPoint3x4(particle.position);
                var offset = destination - (Vector2)local;
                if (this.attractorKills == true && offset.magnitude <= this.attractorRadius) {
                    particle.remainingLifetime = -1f;
                    captured++;
                } else {
                    var acceleration = offset.normalized * (this.attractorStrength * deltaTime * this.simulationSpeed);
                    particle.velocity += inverse.MultiplyVector(acceleration);
                }

                this.attractionBuffer[i] = particle;
            }

            this.SetParticles(this.attractionBuffer, count, systemIndex: index);
            this.SendArrival(target, captured, index, destination);

        }

        private void RequireCpuControl() {

            if (this.backend is VfxGraphParticleBackend || this.prefab != null && this.requestedMode == ParticleRenderMode.VFXGraph) {
                throw new NotSupportedException("VFXGraph does not expose this particle-control API.");
            }

        }

        private void ApplyRandomSeed() {

            if (this.randomSeed.HasValue == false && this.backend is NativeParticleBackend unseeded) {
                unseeded.RestoreSeedPolicy();
            }
            if (this.randomSeed.HasValue == false || this.backend is VfxGraphParticleBackend) {
                return;
            }

            var seed = this.randomSeed.Value;
            this.emissionRandom = new ParticleRandom(unchecked((int)seed));
            if (this.backend is NativeParticleBackend native) {
                for (var i = 0; i < native.systems.Length; i++) {
                    var system = native.systems[i];
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    system.useAutoRandomSeed = false;
                    var systemSeed = unchecked(seed + (uint)i);
                    system.randomSeed = systemSeed == 0 ? 1u : systemSeed;
                }
            }

        }

        private void CancelAsyncPlayback() {

            var pending = this.asyncPlayback;
            this.asyncPlayback = null;
            pending?.TrySetCanceled();

        }

    }

}
