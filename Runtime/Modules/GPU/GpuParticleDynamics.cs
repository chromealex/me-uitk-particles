using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace ME.UITK.Particles {

    // Stateful modules run exclusively on the GPU. Readback is reserved for the public particle API.
    internal sealed class GpuParticleDynamics : IDisposable {

        private struct State {

            internal Vector4 positionLife, velocityLifetime, sizeRotationSeed;

        }

        private readonly ComputeShader compute;
        private readonly ComputeBuffer buffer, alive;
        private readonly RenderTexture texture;
        private readonly State[] states;
        private readonly uint[] zero = { 0u, };
        private int count;
        private float time;
        private uint generation, noiseSeed;
        private bool disposed, pending;
        internal uint? particleCount { get; private set; }
        internal bool supportsStateCapture { get; private set; }
        internal float accelerationBound { get; private set; }
        internal float planeBound { get; private set; }

        internal static bool Enabled(ParticleSystem ps) {

            return ps.limitVelocityOverLifetime.enabled == true || ps.collision.enabled == true
                || ps.trigger.enabled == true || ps.externalForces.enabled == true || SpatialNoise(ps) == true;

        }

        internal static bool SpatialNoise(ParticleSystem ps) {

            return ps.noise.enabled == true && ps.trails.enabled == false && ps.subEmitters.enabled == false;

        }

        private static bool Constant(ParticleSystem.MinMaxCurve value) {

            return value.mode == ParticleSystemCurveMode.Constant && float.IsFinite(value.constant) == true;

        }

        private static bool Zero(ParticleSystem.MinMaxCurve value) {

            return Constant(value) == true && value.constant == 0f;

        }

        internal static string Check(ParticleSystem ps) {

            if (Enabled(ps) == false) {
                return null;
            }

            if (SystemInfo.supportsComputeShaders == false || SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBFloat) == false
                || Resources.Load<ComputeShader>("ParticleDynamics") == null) {
                return "GPU dynamics require compute shaders and a writable RGBAFloat texture";
            }

            if (ps.trails.enabled == true || ps.subEmitters.enabled == true) {
                return "GPU dynamics cannot yet combine with Trails or parent Sub Emitters in the same system";
            }

            var limit = ps.limitVelocityOverLifetime;
            if (limit.enabled == true && (limit.space != ParticleSystemSimulationSpace.Local || Constant(limit.drag) == false
                || (limit.separateAxes == true ? Constant(limit.limitX) == false || Constant(limit.limitY) == false || Constant(limit.limitZ) == false : Constant(limit.limit) == false))) {
                return "GPU Limit Velocity requires Local space and constant limits/drag";
            }

            var collision = ps.collision;
            if (collision.enabled == true && (collision.type != ParticleSystemCollisionType.Planes || collision.planeCount > 8
                || Constant(collision.bounce) == false || Constant(collision.dampen) == false || Constant(collision.lifetimeLoss) == false
                || collision.sendCollisionMessages == true || collision.colliderForce != 0f)) {
                return "GPU Collision supports up to 8 snapshot planes and constant Bounce/Dampen/Lifetime Loss; World, messages and Collider Force are unavailable";
            }

            var trigger = ps.trigger;
            if (trigger.enabled == true) {
                if (trigger.colliderCount > 8 || trigger.inside == ParticleSystemOverlapAction.Callback || trigger.outside == ParticleSystemOverlapAction.Callback
                    || trigger.enter == ParticleSystemOverlapAction.Callback || trigger.exit == ParticleSystemOverlapAction.Callback) {
                    return "GPU Triggers support up to 8 snapshot SphereColliders with Kill/Ignore actions; Callback is unavailable";
                }

                for (var i = 0; i < trigger.colliderCount; i++) {
                    var collider = trigger.GetCollider(i);
                    if (collider != null && collider is SphereCollider == false) {
                        return "GPU Triggers require SphereCollider geometry";
                    }
                }
            }

            var forces = ps.externalForces;
            if (forces.enabled == true) {
                if (forces.influenceFilter != ParticleSystemGameObjectFilter.List || forces.influenceCount > 8 || Constant(forces.multiplierCurve) == false) {
                    return "GPU External Forces require an explicit List of up to 8 fields and a constant multiplier";
                }

                for (var i = 0; i < forces.influenceCount; i++) {
                    var field = forces.GetInfluence(i);
                    if (field != null && (field.shape != ParticleSystemForceFieldShape.Sphere || Constant(field.directionX) == false
                        || Constant(field.directionY) == false || Constant(field.directionZ) == false || Zero(field.gravity) == false
                        || Zero(field.drag) == false || Zero(field.rotationSpeed) == false || Zero(field.rotationAttraction) == false || field.vectorField != null)) {
                        return "GPU External Forces support snapshot spherical directional wind; Gravity, Drag, vortex and vector fields are unavailable";
                    }
                }
            }

            return null;

        }

        internal GpuParticleDynamics(ParticleSystem ps, Material material, int capacity) {

            this.supportsStateCapture = SpatialNoise(ps) == true && ps.limitVelocityOverLifetime.enabled == false
                && ps.collision.enabled == false && ps.trigger.enabled == false && ps.externalForces.enabled == false;
            this.compute = UnityEngine.Object.Instantiate(Resources.Load<ComputeShader>("ParticleDynamics"));
            this.compute.hideFlags = HideFlags.HideAndDontSave;
            this.states = new State[capacity];
            this.buffer = new ComputeBuffer(capacity, 48);
            this.alive = new ComputeBuffer(1, sizeof(uint));
            this.texture = new RenderTexture(512, ((capacity + 511) / 512) * 3, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear) {
                name = "Particle GPU state", enableRandomWrite = true, filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave,
            };
            this.texture.Create();
            this.compute.SetBuffer(0, "_Particles", this.buffer);
            this.compute.SetBuffer(0, "_Alive", this.alive);
            this.compute.SetTexture(0, "_StateTexture", this.texture);
            this.compute.SetTexture(0, "_Motion", material.GetTexture("_Motion") ?? Texture2D.blackTexture);
            this.compute.SetTexture(0, "_Appearance", material.GetTexture("_Appearance") ?? Texture2D.blackTexture);
            foreach (var property in new[] { "_MotionEnabled", "_NoiseSettings", "_OctaveSettings", "_Gravity", "_RotationSpeed", }) {
                this.compute.SetVector(property, material.GetVector(property));
            }

            var limit = ps.limitVelocityOverLifetime;
            this.compute.SetVector("_Limit", new Vector4(Mathf.Max(0f, limit.separateAxes == true ? limit.limitX.constant : limit.limit.constant),
                Mathf.Max(0f, limit.limitY.constant), Mathf.Max(0f, limit.limitZ.constant), limit.multiplyDragByParticleVelocity == true ? 1f : 0f));
            this.compute.SetVector("_LimitSettings", new Vector4(limit.enabled == true ? 1f : 0f, limit.separateAxes == true ? 1f : 0f,
                limit.drag.constant, limit.multiplyDragByParticleSize == true ? 1f : 0f));
            var collision = ps.collision;
            this.compute.SetVector("_Collision", new Vector4(collision.bounce.constant, collision.dampen.constant, collision.lifetimeLoss.constant, collision.radiusScale));
            var trigger = ps.trigger;
            this.compute.SetVector("_KillSpeed", new Vector4(collision.minKillSpeed, collision.maxKillSpeed, trigger.radiusScale, limit.dampen));
            this.compute.SetVector("_TriggerActions", trigger.enabled == true ? new Vector4(Kill(trigger.inside), Kill(trigger.outside), Kill(trigger.enter), Kill(trigger.exit)) : Vector4.zero);
            var planes = new Vector4[8];
            var spheres = new Vector4[8];
            var positions = new Vector4[8];
            var winds = new Vector4[8];
            var planeCount = 0;
            var sphereCount = 0;
            var forceCount = 0;
            if (collision.enabled == true) {
                for (var i = 0; i < collision.planeCount; i++) {
                    var plane = collision.GetPlane(i);
                    if (plane == null) {
                        continue;
                    }

                    var point = ps.transform.InverseTransformPoint(plane.position);
                    var normal = ps.transform.localToWorldMatrix.transpose.MultiplyVector(plane.up).normalized;
                    planes[planeCount++] = new Vector4(normal.x, normal.y, normal.z, Vector3.Dot(normal, point));
                    this.planeBound = Mathf.Max(this.planeBound, point.magnitude);
                }
            }

            if (trigger.enabled == true) {
                for (var i = 0; i < trigger.colliderCount; i++) {
                    var sphere = trigger.GetCollider(i) as SphereCollider;
                    if (sphere == null || sphere.enabled == false) {
                        continue;
                    }

                    var center = ps.transform.InverseTransformPoint(sphere.transform.TransformPoint(sphere.center));
                    var scale = ps.transform.worldToLocalMatrix * sphere.transform.localToWorldMatrix;
                    var radius = sphere.radius * Mathf.Max(scale.MultiplyVector(Vector3.right).magnitude, scale.MultiplyVector(Vector3.up).magnitude, scale.MultiplyVector(Vector3.forward).magnitude);
                    spheres[sphereCount++] = new Vector4(center.x, center.y, center.z, radius);
                }
            }

            var forces = ps.externalForces;
            if (forces.enabled == true) {
                for (var i = 0; i < forces.influenceCount; i++) {
                    var field = forces.GetInfluence(i);
                    if (field == null || field.enabled == false) {
                        continue;
                    }

                    var position = ps.transform.InverseTransformPoint(field.transform.position);
                    var wind = ps.transform.InverseTransformVector(field.transform.TransformDirection(new Vector3(field.directionX.constant, field.directionY.constant, field.directionZ.constant))) * forces.multiplierCurve.constant;
                    var fieldMatrix = ps.transform.worldToLocalMatrix * field.transform.localToWorldMatrix;
                    var fieldScale = Mathf.Max(fieldMatrix.MultiplyVector(Vector3.right).magnitude, fieldMatrix.MultiplyVector(Vector3.up).magnitude, fieldMatrix.MultiplyVector(Vector3.forward).magnitude);
                    positions[forceCount] = new Vector4(position.x, position.y, position.z, field.endRange * fieldScale);
                    winds[forceCount++] = new Vector4(wind.x, wind.y, wind.z, field.startRange * fieldScale);
                    this.accelerationBound += wind.magnitude;
                }
            }

            this.compute.SetInt("_PlaneCount", planeCount);
            this.compute.SetInt("_TriggerCount", sphereCount);
            this.compute.SetInt("_ForceCount", forceCount);
            this.compute.SetVectorArray("_Planes", planes);
            this.compute.SetVectorArray("_Spheres", spheres);
            this.compute.SetVectorArray("_FieldPositions", positions);
            this.compute.SetVectorArray("_FieldForces", winds);
            material.SetTexture("_DynamicsTexture", this.texture);
            material.SetVector("_DynamicsSize", new Vector4(this.texture.width, this.texture.height, 0f, 0f));
            material.SetFloat("_DynamicsEnabled", 1f);

        }

        private static float Kill(ParticleSystemOverlapAction action) {

            return action == ParticleSystemOverlapAction.Kill ? 1f : 0f;

        }

        internal void SetNoiseSeed(uint seed) {

            this.noiseSeed = seed;
            this.compute.SetInt("_NoiseSeed", unchecked((int)seed));

        }

        internal void Upload(ParticleSystem.Particle[] particles, int count, float time) {

            this.count = count;
            this.time = time;
            this.generation++;
            this.particleCount = null;
            for (var i = 0; i < count; i++) {
                var particle = particles[i];
                this.states[i] = new State {
                    positionLife = new Vector4(particle.position.x, particle.position.y, particle.position.z, particle.remainingLifetime),
                    velocityLifetime = new Vector4(particle.velocity.x, particle.velocity.y, particle.velocity.z, particle.startLifetime),
                    sizeRotationSeed = new Vector4(GpuParticleEmitter.Width(particle), particle.rotation * Mathf.Deg2Rad, particle.angularVelocity * Mathf.Deg2Rad, GpuParticleMotion.Random(particle.randomSeed)),
                };
            }

            if (count > 0) {
                this.buffer.SetData(this.states, 0, 0, count);
            }

            this.Step(time, true);

        }

        internal void Step(float time, bool force = false) {

            if (force == false && time <= this.time) {
                return;
            }

            var delta = Mathf.Max(0f, time - this.time);
            this.time = time;
            if (this.count == 0) {
                this.particleCount = 0u;
                return;
            }

            this.alive.SetData(this.zero);
            this.compute.SetInt("_Count", this.count);
            this.compute.SetFloat("_Delta", delta);
            this.compute.SetFloat("_NoiseTime", time - delta);
            this.compute.Dispatch(0, (this.count + 63) / 64, 1, 1);
            if (this.pending == false && SystemInfo.supportsAsyncGPUReadback == true) {
                this.pending = true;
                var requestGeneration = this.generation;
                AsyncGPUReadback.Request(this.alive, request => {
                    this.pending = false;
                    if (this.disposed == false && this.generation == requestGeneration && request.hasError == false) {
                        this.particleCount = request.GetData<uint>()[0];
                    }
                });
            }

        }

        internal Action CaptureState() {

            var savedCount = this.count;
            var savedTime = this.time;
            var savedSeed = this.noiseSeed;
            var savedStates = new State[savedCount];
            if (savedCount > 0) {
                this.buffer.GetData(savedStates, 0, 0, savedCount);
            }
            var live = 0u;
            foreach (var state in savedStates) {
                if (state.positionLife.w > 0f) {
                    live++;
                }
            }
            return () => {
                this.generation++;
                this.count = savedCount;
                this.time = savedTime;
                this.SetNoiseSeed(savedSeed);
                if (savedCount > 0) {
                    this.buffer.SetData(savedStates, 0, 0, savedCount);
                }
                this.Step(savedTime, true);
                this.particleCount = live;
            };

        }

        internal void Read(ParticleSystem.Particle[] particles) {

            if (this.count == 0) {
                return;
            }

            this.buffer.GetData(this.states, 0, 0, this.count);
            var aliveCount = 0u;
            for (var i = 0; i < this.count; i++) {
                var state = this.states[i];
                particles[i].position = state.positionLife;
                particles[i].remainingLifetime = state.positionLife.w;
                particles[i].velocity = state.velocityLifetime;
                particles[i].rotation = state.sizeRotationSeed.y * Mathf.Rad2Deg;
                particles[i].angularVelocity = state.sizeRotationSeed.z * Mathf.Rad2Deg;
                if (state.positionLife.w > 0f) {
                    aliveCount++;
                }
            }

            this.particleCount = aliveCount;

        }

        public void Dispose() {

            this.disposed = true;
            this.buffer.Dispose();
            this.alive.Dispose();
            this.texture.Release();
            ParticleEnvironment.Destroy(this.texture);
            ParticleEnvironment.Destroy(this.compute);

        }

    }

}
