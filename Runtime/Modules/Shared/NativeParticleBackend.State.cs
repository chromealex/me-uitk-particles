using System;
using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    internal abstract partial class NativeParticleBackend {

        private sealed class SystemState {

            internal ParticleSystem system;
            internal ParticleSystem.PlaybackState playback;
            internal ParticleSystem.Particle[] particles;
            internal ParticleSystem.Trails trails;
            internal readonly List<Vector4> custom1 = new(), custom2 = new();
            internal bool playing, paused, emission, autoSeed;
            internal uint seed;

        }

        internal Action CaptureState(out Action validate) {

            var states = new SystemState[this.systems.Length];
            var origin = this.instance.transform.position;
            for (var i = 0; i < states.Length; i++) {
                var system = this.systems[i];
                var state = new SystemState {
                    system = system,
                    playback = system.GetPlaybackState(),
                    particles = new ParticleSystem.Particle[system.particleCount],
                    trails = system.GetTrails(),
                    playing = system.isPlaying,
                    paused = system.isPaused,
                    emission = system.emission.enabled,
                    autoSeed = system.useAutoRandomSeed,
                    seed = system.randomSeed,
                };
                system.GetParticles(state.particles);
                system.GetCustomParticleData(state.custom1, ParticleSystemCustomData.Custom1);
                system.GetCustomParticleData(state.custom2, ParticleSystemCustomData.Custom2);
                states[i] = state;
            }

            var wasDraining = this.draining;
            var resume = (bool[])this.resumeSystems.Clone();
            var emissions = (bool[])this.emissionEnabled.Clone();
            validate = () => {
                if (this.instance == null || this.instance.transform.position != origin || this.systems.Length != states.Length) {
                    throw new InvalidOperationException("The native instance moved or changed since this checkpoint.");
                }
                for (var i = 0; i < states.Length; i++) {
                    if (states[i].system == null || this.systems[i] != states[i].system) {
                        throw new InvalidOperationException("The particle system hierarchy changed since this checkpoint.");
                    }
                }
            };
            return () => {
                this.draining = wasDraining;
                this.resumeSystems = (bool[])resume.Clone();
                this.emissionEnabled = (bool[])emissions.Clone();
                foreach (var state in states) {
                    var system = state.system;
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var emission = system.emission;
                    emission.enabled = state.emission;
                    system.randomSeed = state.seed;
                    system.useAutoRandomSeed = state.autoSeed;
                    if (state.playing == true || state.paused == true) {
                        system.Play(false);
                    }
                    if (state.paused == true) {
                        system.Pause(false);
                    }
                    system.SetParticlesAndTrails(state.particles, state.trails, state.particles.Length);
                    system.SetCustomParticleData(state.custom1, ParticleSystemCustomData.Custom1);
                    system.SetCustomParticleData(state.custom2, ParticleSystemCustomData.Custom2);
                    system.SetPlaybackState(state.playback);
                }
            };

        }

    }

}
