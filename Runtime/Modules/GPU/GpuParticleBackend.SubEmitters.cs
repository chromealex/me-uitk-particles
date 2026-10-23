using System;
using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    internal sealed partial class GpuParticleBackend {

        private readonly List<GpuParticleBackend> children = new();
        private readonly List<SubEmission> pendingDeaths = new();
        private int nextDeath;

        private struct SubEmission {

            internal float time;
            internal int entry;
            internal ParticleSystem.Particle parent;

        }

        internal uint systemCount => (uint)this.children.Count + 1u;

        internal GpuParticleBackend GetSystem(int index) {

            if (index < 0 || index > this.children.Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return index == 0 ? this : this.children[index - 1];

        }

        private void InitializeChildren() {

            using var lease = UnityEngine.Pool.ListPool<ParticleSystem>.Get(out var systems);
            this.sourceSystem.GetComponentsInChildren(true, systems);
            foreach (var system in systems) {
                if (system == this.sourceSystem) {
                    continue;
                }

                var child = new GpuParticleBackend(this.owner);
                this.children.Add(child);
                child.InitializeSystem(system);
                child.instance.gameObject.transform.SetParent(this.instance.gameObject.transform, false);
                child.instance.gameObject.transform.localPosition = Vector3.zero;
                child.instance.gameObject.transform.localRotation = Quaternion.identity;
                child.instance.gameObject.transform.localScale = Vector3.one;
            }

        }

        private Bounds GroupBounds() {

            var result = this.bounds;
            foreach (var child in this.children) {
                result.Encapsulate(child.bounds);
            }

            return new Bounds(Vector3.Scale(result.center, this.owner.systemScale), Vector3.Scale(result.size, this.owner.systemScale));

        }

        private uint GroupParticleCount() {

            var result = this.ownParticleCount;
            foreach (var child in this.children) {
                result += child.ownParticleCount;
            }

            return result;

        }

        private bool HasActiveChildren() {

            if (this.nextDeath < this.pendingDeaths.Count) {
                return true;
            }

            foreach (var child in this.children) {
                if (child.isAlive == true) {
                    return true;
                }
            }

            return false;

        }

        private void ResetChildren() {

            this.pendingDeaths.Clear();
            this.nextDeath = 0;
            foreach (var child in this.children) {
                child.Play();
                child.SetParticles(Array.Empty<ParticleSystem.Particle>(), 0, 0);
            }

        }

        internal void ClearSystems() {

            this.pendingDeaths.Clear();
            this.nextDeath = 0;
            this.trails?.Clear();
            this.SetParticles(Array.Empty<ParticleSystem.Particle>(), 0, 0);
            foreach (var child in this.children) {
                child.SetParticles(Array.Empty<ParticleSystem.Particle>(), 0, 0);
            }

        }

        private void StopChildren() {

            this.pendingDeaths.Clear();
            this.nextDeath = 0;
            foreach (var child in this.children) {
                child.Stop();
            }

        }

        private void DisposeChildren() {

            foreach (var child in this.children) {
                child.Dispose();
            }

            this.children.Clear();

        }

        private void EmitBirth(ParticleSystem.Particle parent) {

            var subs = this.sourceSystem.subEmitters;
            if (this.children.Count == 0 || subs.enabled == false) {
                return;
            }

            for (var entry = 0; entry < subs.subEmittersCount; entry++) {
                if (subs.GetSubEmitterType(entry) == ParticleSystemSubEmitterType.Birth) {
                    this.EmitSubSystem(entry, parent, this.elapsed);
                }
            }

        }

        private void ScheduleDeaths() {

            this.pendingDeaths.Clear();
            this.nextDeath = 0;
            var subs = this.sourceSystem.subEmitters;
            if (this.children.Count == 0 || subs.enabled == false) {
                return;
            }

            for (var i = 0; i < this.liveCount; i++) {
                var parent = this.particles[i];
                if (parent.remainingLifetime <= 0f) {
                    continue;
                }

                for (var entry = 0; entry < subs.subEmittersCount; entry++) {
                    if (subs.GetSubEmitterType(entry) == ParticleSystemSubEmitterType.Death) {
                        this.pendingDeaths.Add(new SubEmission {
                            time = this.stateTime + parent.remainingLifetime,
                            entry = entry,
                            parent = this.EvaluateParticle(parent, parent.remainingLifetime),
                        });
                    }
                }
            }

            this.pendingDeaths.Sort((a, b) => a.time.CompareTo(b.time));

        }

        private void AdvanceChildren() {

            while (this.nextDeath < this.pendingDeaths.Count && this.pendingDeaths[this.nextDeath].time <= this.elapsed) {
                var emission = this.pendingDeaths[this.nextDeath++];
                this.EmitSubSystem(emission.entry, emission.parent, emission.time);
            }

            foreach (var child in this.children) {
                child.elapsed = this.elapsed;
                child.dynamics?.Step(child.elapsed);
                child.material.SetFloat(GpuParticleBackend.elapsedId, child.elapsed);
                child.trails?.SetTime(child.elapsed);
                child.lights?.SetTime(child.elapsed);
            }

        }

        private ParticleSystem.Particle CreateSubParticle(uint seed, System.Random random, Quaternion orientation) {

            var main = this.sourceSystem.main;
            var shape = this.sourceSystem.shape;
            var lifetime = GpuParticleEmitter.Lifetime(this.sourceSystem, main.startLifetime.Evaluate(0f, (float)random.NextDouble()), GpuParticleMotion.Random(seed));
            GpuParticleEmitter.Shape(shape, random, out var position, out var direction);

            var velocity = orientation * direction * main.startSpeed.Evaluate(0f, (float)random.NextDouble());
            var size = GpuParticleEmitter.Size(main, (float)random.NextDouble());
            var rotation = main.startRotation.Evaluate(0f, (float)random.NextDouble());
            var color = main.startColor.Evaluate(0f, (float)random.NextDouble());
            var motionRandom = GpuParticleMotion.Random(seed);
            this.motion.Evaluate(0f, lifetime, motionRandom, out _, out var moduleVelocity, out _, out var moduleRotation);
            velocity += moduleVelocity + GpuParticleEmitter.InitialVelocity(this.sourceSystem, motionRandom);
            return new ParticleSystem.Particle {
                position = orientation * position,
                velocity = velocity,
                angularVelocity = (moduleRotation + this.appearance.RotationRate(velocity.magnitude, motionRandom)) * Mathf.Rad2Deg,
                startLifetime = lifetime,
                remainingLifetime = lifetime,
                startSize3D = size,
                rotation = rotation * Mathf.Rad2Deg,
                startColor = color,
                randomSeed = seed,
            };

        }

        private void EmitSubSystem(int entry, ParticleSystem.Particle parent, float time) {

            var subs = this.sourceSystem.subEmitters;
            var probability = subs.GetSubEmitterEmitProbability(entry);
            if (probability <= 0f || probability < 1f && GpuParticleMotion.Random(unchecked(parent.randomSeed + (uint)entry * 747796405u)) >= probability) {
                return;
            }

            foreach (var child in this.children) {
                if (child.sourceSystem != subs.GetSubEmitterSystem(entry)) {
                    continue;
                }

                child.elapsed = time;
                var before = (int)child.ownParticleCount;
                var available = child.capacity - before;
                if (available <= 0) {
                    break;
                }

                child.Emit(Mathf.Min(child.initialParticles.Length, available), null, rebuild: false);
                var properties = subs.GetSubEmitterProperties(entry);
                var age = Mathf.Clamp01(1f - parent.remainingLifetime / Mathf.Max(parent.startLifetime, .000001f));
                // Native sub-emission aligns the emitter's forward axis with the parent's velocity.
                // Local lifetime modules remain in the child system's space.
                var orientation = parent.velocity.sqrMagnitude > .00000001f
                    ? Quaternion.FromToRotation(Vector3.forward, parent.velocity.normalized) : Quaternion.identity;
                var random = new System.Random(unchecked((int)((child.sampledSeed ?? child.sourceSystem.randomSeed)
                    ^ child.emissionSequence * 747796405u ^ parent.randomSeed * 2891336453u)));
                for (var i = before; i < child.liveCount; i++) {
                    var particle = child.CreateSubParticle(child.particles[i].randomSeed, random, orientation);
                    particle.position += parent.position;
                    if ((properties & ParticleSystemSubEmitterProperties.InheritColor) != 0) {
                        var color = this.sourceSystem.colorOverLifetime;
                        particle.startColor = (Color)particle.startColor * (Color)parent.startColor
                            * (color.enabled == true ? color.color.Evaluate(age) : Color.white);
                    }

                    if ((properties & ParticleSystemSubEmitterProperties.InheritSize) != 0) {
                        var size = this.sourceSystem.sizeOverLifetime;
                        particle.startSize3D = Vector3.Scale(particle.startSize3D, parent.startSize3D) * (size.enabled == true ? size.size.Evaluate(age) : 1f);
                    }

                    if ((properties & ParticleSystemSubEmitterProperties.InheritRotation) != 0) {
                        particle.rotation += parent.rotation;
                    }

                    child.particles[i] = particle;
                }

                child.UpdateMesh();
                break;
            }

        }

    }

}
