using System;
using UnityEngine;

namespace ME.UITK.Particles {

    internal sealed partial class GpuParticleBackend {

        internal bool supportsStateCapture {
            get {
                if (this.dynamics != null && this.dynamics.supportsStateCapture == false) {
                    return false;
                }
                foreach (var child in this.children) {
                    if (child.supportsStateCapture == false) {
                        return false;
                    }
                }
                return true;
            }
        }

        internal Action CaptureState() {

            if (this.supportsStateCapture == false) {
                throw new NotSupportedException("GPU compute dynamics do not expose a complete checkpoint.");
            }

            var initial = (ParticleSystem.Particle[])this.initialParticles.Clone();
            var particles = (ParticleSystem.Particle[])this.particles.Clone();
            var count = this.liveCount;
            var elapsed = this.elapsed;
            var stateTime = this.stateTime;
            var sequence = this.emissionSequence;
            var burstIndex = this.nextBurst;
            var emitBursts = this.automaticBursts;
            var seed = this.sampledSeed;
            var playing = this.playing;
            var visible = this.renderer.enabled;
            var deaths = this.pendingDeaths.ToArray();
            var nextDeath = this.nextDeath;
            var restoreTrails = this.trails?.CaptureState();
            var restoreDynamics = this.dynamics?.CaptureState();
            var childStates = new Action[this.children.Count];
            for (var i = 0; i < childStates.Length; i++) {
                childStates[i] = this.children[i].CaptureState();
            }

            return () => {
                this.initialParticles = (ParticleSystem.Particle[])initial.Clone();
                Array.Copy(particles, this.particles, particles.Length);
                this.liveCount = count;
                this.elapsed = elapsed;
                this.stateTime = stateTime;
                this.emissionSequence = sequence;
                this.nextBurst = burstIndex;
                this.automaticBursts = emitBursts;
                this.sampledSeed = seed;
                this.playing = playing;
                this.pendingDeaths.Clear();
                this.pendingDeaths.AddRange(deaths);
                this.nextDeath = nextDeath;
                this.lastTime = ParticleEnvironment.Now(this.unscaled);
                restoreTrails?.Invoke();
                this.UpdateMesh();
                restoreDynamics?.Invoke();
                this.material.SetFloat(GpuParticleBackend.elapsedId, this.elapsed);
                this.renderer.enabled = visible;
                this.trails?.SetEnabled(visible);
                this.trails?.SetTime(elapsed);
                this.lights?.SetEnabled(visible);
                this.lights?.SetTime(elapsed);
                for (var i = 0; i < childStates.Length; i++) {
                    childStates[i]();
                }
                if (playing == true) {
                    this.presenter?.Play();
                } else {
                    this.presenter?.Stop();
                }
            };

        }

    }

}
