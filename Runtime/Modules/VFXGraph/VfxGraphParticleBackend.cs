using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

namespace ME.UITK.Particles {

    internal sealed class VfxGraphParticleBackend : IParticleBackend {

        private readonly ParticleElement owner;
        private readonly ParticleInstance effectInstance = new();
        private Vector3 prefabScale;
        private VisualEffect[] effects = System.Array.Empty<VisualEffect>();
        internal ParticleTexturePresenter presenter { get; private set; }
        private bool playing;
        private float[] originalSpeeds;
        public uint bakedVertexCount => 0;
        public Bounds renderBounds => this.presenter != null ? this.presenter.renderBounds : default;
        public bool requiresObservedParticles => true;

        public VfxGraphParticleBackend(ParticleElement owner) {

            this.owner = owner;

        }

        public uint particleCount {
            get {
                if (this.playing == false) {
                    return 0;
                }

                var count = 0u;
                foreach (var effect in this.effects) {
                    if (effect != null && effect.isActiveAndEnabled == true) {
                        // Unity supplies an asynchronous GPU readback, not a current-frame count.
                        var alive = (uint)Mathf.Max(0, effect.aliveParticleCount);
                        count += System.Math.Min(alive, uint.MaxValue - count);
                    }
                }

                return count;
            }
        }

        public bool isAlive {
            get {
                foreach (var effect in this.effects) {
                    if (effect != null && effect.isActiveAndEnabled == true
                        && (effect.HasAnySystemAwake() == true || effect.aliveParticleCount > 0)) {
                        return true;
                    }
                }

                return false;
            }
        }

        public static void ValidateBounds(Bounds bounds) {

            var center = bounds.center;
            var size = bounds.size;
            if (float.IsFinite(center.x) == false || float.IsFinite(center.y) == false || float.IsFinite(center.z) == false
                || float.IsFinite(size.x) == false || float.IsFinite(size.y) == false || float.IsFinite(size.z) == false
                || size.x <= 0f || size.y <= 0f || size.z <= 0f) {
                throw new System.ArgumentOutOfRangeException(nameof(bounds), "VFXGraph bounds must have a finite center and finite, positive dimensions.");
            }

        }

        public static void ValidatePrefab(GameObject prefab) {

            if (prefab == null) {
                return;
            }

            using var effectsLease = UnityEngine.Pool.ListPool<VisualEffect>.Get(out var effects);
            prefab.GetComponentsInChildren(true, effects);
            if (effects.Count == 0) {
                throw new System.ArgumentException("VFXGraph mode requires a prefab containing a VisualEffect.", nameof(prefab));
            }

            foreach (var effect in effects) {
                if (effect.visualEffectAsset == null) {
                    throw new System.ArgumentException("Every VisualEffect must have a VFX Graph asset assigned.", nameof(prefab));
                }
            }

            if (prefab.GetComponentInChildren<ParticleSystem>(true) != null) {
                throw new System.ArgumentException("VFXGraph mode requires a VFX-only prefab. Use the existing modes for ParticleSystem prefabs.", nameof(prefab));
            }

            if (SystemInfo.supportsComputeShaders == false) {
                throw new System.NotSupportedException("VFXGraph mode requires compute shader support.");
            }

        }

        public void Initialize(GameObject prefab) {

            VfxGraphParticleBackend.ValidatePrefab(prefab);
            this.effectInstance.Create(prefab);
            this.prefabScale = this.effectInstance.gameObject.transform.localScale;
            this.SetSystemScale(this.owner.systemScale);
            this.effects = this.effectInstance.gameObject.GetComponentsInChildren<VisualEffect>(true);
            this.originalSpeeds = new float[this.effects.Length];
            for (var i = 0; i < this.effects.Length; i++) {
                this.originalSpeeds[i] = this.effects[i].playRate;
            }

            this.SetSimulationSpeed(this.owner.simulationSpeed);
            this.StopEffects();
            this.presenter = new ParticleTexturePresenter(this.owner, this.effectInstance.gameObject, this.GetBounds);
            this.presenter.Initialize();

        }

        private Bounds GetBounds() {

            var bounds = this.owner.gpuBounds;
            return new Bounds(Vector3.Scale(bounds.center, this.owner.systemScale), Vector3.Scale(bounds.size, this.owner.systemScale));

        }

        public void SetSimulationSpeed(float value) {

            for (var i = 0; i < this.effects.Length; i++) {
                if (this.effects[i] != null) {
                    this.effects[i].playRate = this.originalSpeeds[i] * value;
                }
            }

        }

        public void SetSystemScale(Vector3 value) {

            this.effectInstance.gameObject.transform.localScale = Vector3.Scale(this.prefabScale, value);

        }

        public void Play() {

            this.playing = true;
            foreach (var effect in this.effects) {
                if (effect != null) {
                    effect.enabled = true;
                    effect.pause = Application.isPlaying == false;
                    // Reinit clears the previous simulation and sends the initial event once.
                    effect.Reinit();
                }
            }

            this.presenter.Play();

        }

        private void StopEffects() {

            this.playing = false;
            foreach (var effect in this.effects) {
                if (effect != null) {
                    effect.Stop();
                    effect.pause = true;
                    effect.enabled = false;
                }
            }

        }

        public void Stop() {

            this.StopEffects();
            this.presenter.Stop();

        }

        public void Pause() {

            foreach (var effect in this.effects) {
                if (effect != null) {
                    effect.pause = true;
                }
            }

        }

        public void Resume() {

            foreach (var effect in this.effects) {
                if (effect != null) {
                    effect.pause = Application.isPlaying == false;
                }
            }

        }

        public void StopEmitting() {

            foreach (var effect in this.effects) {
                if (effect != null) {
                    effect.Stop();
                }
            }

        }

        public void Tick() {

            if (Application.isPlaying == false && this.playing == true && this.owner.isPaused == false) {
                foreach (var effect in this.effects) {
                    if (effect != null && effect.isActiveAndEnabled == true) {
                        effect.Simulate(this.owner.editorDeltaTime);
                    }
                }

                // VFX simulation commands are processed by the player loop, even in Edit Mode.
                ParticleEnvironment.requestEditorUpdate?.Invoke();
            }

            this.presenter.PresentTextureFrame();

        }

        public void Draw(MeshGenerationContext context) {

        }

        public void Dispose() {

            this.playing = false;
            if (this.presenter != null) {
                this.presenter.Dispose();
                this.presenter = null;
            }

            this.effectInstance.Dispose();
            this.effects = System.Array.Empty<VisualEffect>();

        }

    }

}
