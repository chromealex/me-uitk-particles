using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private sealed class ElementCheckpoint {

            internal bool requestedPlay, paused, completed, pending, observed, automatic, jobs, emissionStopped;
            internal float speed, pixels, visualScale, rate, attractorStrength, attractorRadius;
            internal Vector3 systemScale;
            internal Vector2 position;
            internal Color tint;
            internal uint? seed;
            internal uint placementState, nextFlightSeed, jobThreshold;
            internal double elapsed, remainder;
            internal ParticleCompletionSettings configuredCompletion, playbackCompletion;
            internal VisualElement emissionArea, attractorTarget;
            internal string declarativeEmissionArea, declarativeAttractor;
            internal int emissionIndex, attractorIndex;
            internal bool attractorKills, renderOnTop;
            internal ParticleFlight[] flights;
            internal Action restoreBackend;

        }

        /// <summary>Native systems, analytic GPU and standalone spatial Noise support checkpoints. Other compute dynamics and VFX Graph do not.</summary>
        public bool supportsStateCapture => this.backend is NativeParticleBackend
            || this.backend is GpuParticleBackend shader && shader.supportsStateCapture == true;

        /// <summary>Captures an immutable in-memory checkpoint for this backend instance, including control state.</summary>
        public ParticleState CaptureState() {

            if (this.backend == null) {
                throw new InvalidOperationException("CaptureState requires an initialized backend.");
            }
            if (this.supportsStateCapture == false) {
                throw new NotSupportedException("This backend cannot capture its complete simulation state.");
            }

            Action validate = null;
            var restore = this.backend is NativeParticleBackend native ? native.CaptureState(out validate)
                : ((GpuParticleBackend)this.backend).CaptureState();
            var now = ParticleEnvironment.Now(this.playbackCompletion.clock == ParticleCompletionClock.Unscaled);
            var elapsed = this.automaticSimulation == false ? this.manualElapsed
                : (this.isPaused == true ? this.pauseStartTime : now) - this.playbackStartTime;
            var state = new ElementCheckpoint {
                renderOnTop = this.renderOnTop,
                requestedPlay = this.requestedPlay,
                paused = this.isPaused,
                completed = this.hasCompletedPlayback,
                pending = this.completionPending,
                observed = this.observedParticles,
                automatic = this.automaticSimulation,
                speed = this.simulationSpeed,
                pixels = this.pixelsPerUnit,
                visualScale = this.visualScale,
                systemScale = this.systemScale,
                position = this.positionOffset,
                tint = this.tint,
                seed = this.randomSeed,
                placementState = this.emissionRandom.state,
                nextFlightSeed = this.nextFlightSeed,
                elapsed = Math.Max(0d, elapsed),
                configuredCompletion = this.configuredCompletion,
                playbackCompletion = this.playbackCompletion,
                rate = this.emissionRate,
                remainder = this.emissionRemainder,
                emissionArea = this.emissionArea,
                declarativeEmissionArea = this.declarativeEmissionArea,
                emissionIndex = this.emissionSystemIndex,
                emissionStopped = this.managedEmissionStopped,
                attractorTarget = this.attractorTarget,
                declarativeAttractor = this.declarativeAttractor,
                attractorStrength = this.attractorStrength,
                attractorRadius = this.attractorRadius,
                attractorIndex = this.attractorSystemIndex,
                attractorKills = this.attractorKills,
                flights = this.CopyFlights(),
                jobs = this.useJobs,
                jobThreshold = this.jobVertexThreshold,
                restoreBackend = restore,
            };
            return new ParticleState(this, this.backend, this.particleCount, validate, () => this.ApplyCheckpoint(state));

        }

        /// <summary>
        /// Restores a checkpoint to the same element and backend instance. Replacement, detach or Dispose invalidate it.
        /// Cancels an outstanding PlayAsync wait; event subscriptions and external UI/scene state are not rewound.
        /// </summary>
        public void RestoreState(ParticleState state) {

            if (state == null) {
                throw new ArgumentNullException(nameof(state));
            }
            if (state.owner != this || state.backend != this.backend || this.backend == null) {
                throw new InvalidOperationException("A checkpoint belongs to one element and one backend instance.");
            }
            if (this.supportsStateCapture == false) {
                throw new NotSupportedException("This backend cannot restore a checkpoint.");
            }

            state.validate?.Invoke();
            this.CancelAsyncPlayback();
            this.FinishPresentationTransition();
            this.ClearFlights();
            state.apply();

        }

        private void ApplyCheckpoint(ElementCheckpoint state) {

            this.SetRenderOnTop(state.renderOnTop);
            this.automaticSimulationEnabled = state.automatic;
            this.isPaused = state.paused;
            this.requestedPlay = state.requestedPlay;
            this.randomSeed = state.seed;
            this.SetVisualScale(state.visualScale);
            this.SetSystemScale(state.systemScale);
            this.SetPosition(state.position);
            this.SetTint(state.tint);
            this.pixelsPerUnit = state.pixels;
            this.simulationSpeed = state.speed;
            this.useJobs = state.jobs;
            this.jobVertexThreshold = state.jobThreshold;
            if (state.requestedPlay == true) {
                this.backend.Play();
            } else {
                this.backend.Stop();
            }
            state.restoreBackend();
            this.emissionRandom.state = state.placementState;
            this.nextFlightSeed = state.nextFlightSeed;
            this.hasCompletedPlayback = state.completed;
            this.completionPending = state.pending;
            this.observedParticles = state.observed;
            this.configuredCompletion = state.configuredCompletion;
            this.playbackCompletion = state.playbackCompletion;
            this.manualElapsed = state.elapsed;
            var now = ParticleEnvironment.Now(state.playbackCompletion.clock == ParticleCompletionClock.Unscaled);
            this.playbackStartTime = now - state.elapsed;
            this.pauseStartTime = now;
            this.playbackStartFrame = Time.frameCount;
            this.lastEditorTime = ParticleEnvironment.Now(true);
            this.lastAttractorTime = ParticleEnvironment.Now(false);
            this.emissionRate = state.rate;
            this.emissionRemainder = state.remainder;
            this.emissionArea = state.emissionArea;
            this.declarativeEmissionArea = state.declarativeEmissionArea;
            this.emissionSystemIndex = state.emissionIndex;
            this.managedEmissionStopped = state.emissionStopped;
            this.attractorTarget = state.attractorTarget;
            this.declarativeAttractor = state.declarativeAttractor;
            this.attractorStrength = state.attractorStrength;
            this.attractorRadius = state.attractorRadius;
            this.attractorSystemIndex = state.attractorIndex;
            this.attractorKills = state.attractorKills;
            foreach (var flight in state.flights) {
                this.flights.Add(ParticleElement.CopyFlight(flight));
            }
            this.ResetFlightClocks();
            if (state.requestedPlay == true) {
                this.ticker.Resume();
            } else {
                this.ticker.Pause();
            }
            this.MarkDirtyRepaint();

        }

        private ParticleFlight[] CopyFlights() {

            var result = new ParticleFlight[this.flights.Count];
            var now = ParticleEnvironment.Now(false);
            for (var i = 0; i < result.Length; i++) {
                var copy = ParticleElement.CopyFlight(this.flights[i]);
                // Keep the exact last-rendered particle state; the next update begins at capture time.
                copy.lastTime = now;
                result[i] = copy;
            }
            return result;

        }

        private static ParticleFlight CopyFlight(ParticleFlight source) {

            var copy = new ParticleFlight {
                destination = source.destination,
                start = source.start,
                duration = source.duration,
                elapsed = source.elapsed,
                lastTime = source.lastTime,
                systemIndex = source.systemIndex,
            };
            copy.seeds.UnionWith(source.seeds);
            return copy;

        }

    }

}
