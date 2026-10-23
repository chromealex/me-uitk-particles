using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal abstract partial class NativeParticleBackend : IParticleBackend {

        protected readonly ParticleElement owner;
        private Vector3 prefabScale;
        private bool[] resumeSystems, emissionEnabled;
        private bool[] originalAutoSeeds;
        private uint[] originalSeeds;
        private bool draining;
        private float[] originalSpeeds;
        private readonly ParticleInstance effectInstance = new();
        protected GameObject instance => this.effectInstance.gameObject;
        internal ParticleSystem[] systems { get; private set; } = System.Array.Empty<ParticleSystem>();
        protected ParticleSystemRenderer[] renderers = System.Array.Empty<ParticleSystemRenderer>();
        private readonly List<ParticleSystem> playbackSystems = new();
        public virtual uint bakedVertexCount { get; protected set; }
        public virtual Bounds renderBounds => default;
        public bool requiresObservedParticles => false;

        protected NativeParticleBackend(ParticleElement owner) {

            this.owner = owner;

        }

        public uint particleCount {
            get {
                var count = 0u;
                foreach (var system in this.systems) {
                    if (system != null) {
                        count += (uint)system.particleCount;
                    }
                }

                return count;
            }
        }

        public bool isAlive {
            get {
                foreach (var system in this.systems) {
                    if (system != null && system.IsAlive(false) == true) {
                        return true;
                    }
                }

                return false;
            }
        }

        public virtual void Initialize(GameObject prefab) {

            this.effectInstance.Create(prefab);
            this.prefabScale = this.instance.transform.localScale;
            this.SetSystemScale(this.owner.systemScale);
            this.systems = this.instance.GetComponentsInChildren<ParticleSystem>(true);
            this.renderers = this.instance.GetComponentsInChildren<ParticleSystemRenderer>(true);
            this.resumeSystems = new bool[this.systems.Length];
            this.emissionEnabled = new bool[this.systems.Length];
            this.originalSpeeds = new float[this.systems.Length];
            this.originalAutoSeeds = new bool[this.systems.Length];
            this.originalSeeds = new uint[this.systems.Length];
            for (var i = 0; i < this.systems.Length; i++) {
                this.originalSpeeds[i] = this.systems[i].main.simulationSpeed;
                this.originalAutoSeeds[i] = this.systems[i].useAutoRandomSeed;
                this.originalSeeds[i] = this.systems[i].randomSeed;
            }

            this.SetSimulationSpeed(this.owner.simulationSpeed);
            foreach (var system in this.systems) {
                var main = system.main;
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                main.stopAction = ParticleSystemStopAction.None;
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (this.IsSubEmitter(system) == false) {
                    this.playbackSystems.Add(system);
                }
            }

        }

        public void SetSimulationSpeed(float value) {

            for (var i = 0; i < this.systems.Length; i++) {
                if (this.systems[i] != null) {
                    var main = this.systems[i].main;
                    main.simulationSpeed = this.originalSpeeds[i] * value;
                }
            }

        }

        internal void Emit(int count, int systemIndex, List<Vector3> positions, ParticleEmitParams overrides = default, List<uint> seeds = null) {

            var system = this.systems[systemIndex];
            var parameters = overrides.ToNative();
            if (positions == null && seeds == null) {
                system.Emit(parameters, count);
            } else {
                for (var i = 0; i < count; i++) {
                    if (positions != null) {
                        parameters.position = positions[i];
                        parameters.applyShapeToPosition = false;
                    }
                    if (seeds != null) {
                        parameters.randomSeed = seeds[i];
                    }
                    system.Emit(parameters, 1);
                }
            }

        }

        internal void RestoreSeedPolicy() {

            for (var i = 0; i < this.systems.Length; i++) {
                var system = this.systems[i];
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.randomSeed = this.originalSeeds[i];
                system.useAutoRandomSeed = this.originalAutoSeeds[i];
            }

        }

        public void SetSystemScale(Vector3 value) {

            this.instance.transform.localScale = Vector3.Scale(this.prefabScale, value);

        }

        public virtual void Play() {

            if (this.draining == true) {
                for (var i = 0; i < this.systems.Length; i++) {
                    if (this.systems[i] != null) {
                        var emission = this.systems[i].emission;
                        emission.enabled = this.emissionEnabled[i];
                    }
                }
            }

            this.draining = false;

            foreach (var system in this.systems) {
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (var system in this.playbackSystems) {
                if (system != null) {
                    system.Play(false);
                }
            }

        }

        public virtual void Stop() {

            foreach (var system in this.systems) {
                if (system != null) {
                    system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }

        }

        public void Pause() {

            for (var i = 0; i < this.systems.Length; i++) {
                var system = this.systems[i];
                this.resumeSystems[i] = system != null && (system.isPlaying == true || system.isPaused == true || system.particleCount > 0);
                if (this.resumeSystems[i] == true) {
                    system.Pause(false);
                }
            }

        }

        public void Resume() {

            for (var i = 0; i < this.systems.Length; i++) {
                var system = this.systems[i];
                if (system != null && this.resumeSystems[i] == true) {
                    system.Play(false);
                    if (this.draining == true) {
                        system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    }
                }
            }

        }

        public void StopEmitting() {

            if (this.draining == true) {
                return;
            }

            this.draining = true;
            for (var i = 0; i < this.systems.Length; i++) {
                var system = this.systems[i];
                if (system == null) {
                    continue;
                }

                var emission = system.emission;
                this.emissionEnabled[i] = emission.enabled;
                emission.enabled = false;
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                if (this.owner.isPaused == true || this.owner.automaticSimulation == false) {
                    system.Pause(false);
                }
            }

        }

        internal Matrix4x4 SimulationToEffectMatrix(int systemIndex) {

            var system = this.systems[systemIndex];
            var main = system.main;
            var matrix = Matrix4x4.identity;
            if (main.simulationSpace != ParticleSystemSimulationSpace.World) {
                var space = main.simulationSpace == ParticleSystemSimulationSpace.Custom ? main.customSimulationSpace : system.transform;
                if (space == null) {
                    throw new System.InvalidOperationException("The custom simulation transform is missing.");
                }

                matrix = space.localToWorldMatrix;
                if (main.simulationSpace == ParticleSystemSimulationSpace.Local && main.scalingMode != ParticleSystemScalingMode.Hierarchy) {
                    var scale = main.scalingMode == ParticleSystemScalingMode.Local ? space.localScale : Vector3.one;
                    matrix = Matrix4x4.TRS(space.position, space.rotation, scale);
                }
            }

            return Matrix4x4.Translate(-this.instance.transform.position) * matrix;

        }

        internal float GetPrefabSpeed(int systemIndex) {

            return this.originalSpeeds[systemIndex];

        }

        internal void SimulateEditor(float deltaTime) {

            foreach (var system in this.playbackSystems) {
                if (system != null && system.gameObject.activeInHierarchy == true) {
                    system.Simulate(deltaTime, false, false, false);
                    // Simulate pauses the system. Explicitly end emission at the duration so
                    // IsAlive does not keep a completed, paused preview alive indefinitely.
                    var main = system.main;
                    if (this.draining == true || main.loop == false && system.time >= main.duration) {
                        system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                    }
                    if (this.owner.automaticSimulation == false && (system.particleCount > 0 || system.isStopped == false)) {
                        system.Pause(false);
                    }
                }
            }

        }

        public virtual void Tick() {

            this.owner.MarkDirtyRepaint();

        }

        public virtual void Draw(MeshGenerationContext context) {

        }

        public virtual void Dispose() {

            this.effectInstance.Dispose();
            this.systems = System.Array.Empty<ParticleSystem>();
            this.renderers = System.Array.Empty<ParticleSystemRenderer>();
            this.playbackSystems.Clear();

        }

        private bool IsSubEmitter(ParticleSystem candidate) {

            foreach (var system in this.systems) {
                var sub = system.subEmitters;
                for (var i = 0; i < sub.subEmittersCount; i++) {
                    if (sub.GetSubEmitterSystem(i) == candidate) {
                        return true;
                    }
                }
            }

            return false;

        }

    }

}
