using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal sealed partial class GpuParticleBackend : IParticleBackend {

        private static readonly int elapsedId = UnityEngine.Shader.PropertyToID("_Elapsed");
        private readonly ParticleElement owner;
        private readonly ParticleInstance instance = new();
        private Material material;
        private Mesh mesh;
        private Texture2D lifetimeTexture;
        private GpuParticleMotion motion;
        private GpuParticleAppearance appearance;
        private GpuParticleDynamics dynamics;
        private GpuParticleTrails trails;
        private GpuParticleLights lights;
        private MeshRenderer renderer;
        internal ParticleTexturePresenter presenter { get; private set; }
        private ParticleSystem.Particle[] initialParticles, particles;
        private ParticleSystem sourceSystem;
        private uint? sampledSeed;
        private int liveCount, capacity;
        private float stateTime, maxLifetimeSize;
        private Vector3 gravity;
        private float duration, speed, elapsed;
        private double lastTime;
        private float speedMultiplier = 1f;
        private uint emissionSequence;
        private bool unscaled, playing;
        private Bounds bounds;
        public uint bakedVertexCount => 0;
        public Bounds renderBounds => this.GroupBounds();
        public bool requiresObservedParticles => false;
        public bool isAlive => this.playing == true && (this.hasPendingBursts == true || (this.elapsed < this.duration && (this.dynamics == null || this.ownParticleCount > 0u)) || this.HasActiveChildren() == true);

        public uint particleCount => this.GroupParticleCount();

        internal uint ownParticleCount {
            get {
                if (this.liveCount > 0 && this.dynamics?.particleCount is uint gpuCount) {
                    return gpuCount;
                }

                var count = 0u;
                for (var i = 0; i < this.liveCount; i++) {
                    if (this.particles[i].remainingLifetime > this.elapsed - this.stateTime) {
                        count++;
                    }
                }

                return count;
            }
        }

        internal GpuParticleBackend(ParticleElement owner) {

            this.owner = owner;

        }

        public void Initialize(GameObject prefab) {

            var reason = GpuParticleCompatibility.Check(prefab);
            if (reason != null) {
                throw new System.ArgumentException(reason, nameof(prefab));
            }

            this.InitializeSystem(prefab.GetComponent<ParticleSystem>());
            this.InitializeChildren();
            this.presenter = new ParticleTexturePresenter(this.owner, this.instance.gameObject, () => this.renderBounds);
            this.presenter.Initialize();

        }

        private void InitializeSystem(ParticleSystem ps) {

            var prefab = ps.gameObject;
            this.sourceSystem = ps;
            var main = ps.main;
            this.unscaled = main.useUnscaledTime;
            this.speed = main.simulationSpeed;
            this.speedMultiplier = this.owner.simulationSpeed;
            this.instance.CreateEmpty(prefab.name);
            this.SetSystemScale(this.owner.systemScale);
            var sourceMaterial = ps.GetComponent<ParticleSystemRenderer>().sharedMaterial;
            this.material = new Material(Resources.Load<UnityEngine.Shader>("ProceduralParticles")) { hideFlags = HideFlags.HideAndDontSave, };
            this.material.mainTexture = sourceMaterial.mainTexture;
            this.material.SetColor("_Color", sourceMaterial.GetColor("_Color"));
            this.material.SetFloat("_ApplyColorSpace", ps.GetComponent<ParticleSystemRenderer>().applyActiveColorSpace == true && QualitySettings.activeColorSpace == ColorSpace.Linear ? 1 : 0);
            this.material.SetFloat("_MaxSize", ps.GetComponent<ParticleSystemRenderer>().maxParticleSize);
            this.gravity = Physics.gravity * main.gravityModifier.constant;
            this.material.SetVector("_Gravity", this.gravity);
            this.motion = new GpuParticleMotion(ps, this.material);
            this.appearance = new GpuParticleAppearance(ps, this.material);
            this.BuildMesh(ps);
            if (GpuParticleDynamics.Enabled(ps) == true) {
                this.dynamics = new GpuParticleDynamics(ps, this.material, this.capacity);
            }
            this.instance.gameObject.AddComponent<MeshFilter>().sharedMesh = this.mesh;
            this.renderer = this.instance.gameObject.AddComponent<MeshRenderer>();
            this.renderer.sharedMaterial = this.material;
            this.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            this.renderer.receiveShadows = false;
            this.renderer.enabled = false;
            if (ps.lights.enabled == true) {
                this.lights = new GpuParticleLights(ps, this.instance.gameObject, this.material);
            }

            if (ps.trails.enabled == true) {
                this.trails = new GpuParticleTrails(ps, this.instance.gameObject, this.material);
            }

        }

        private void BuildMesh(ParticleSystem ps) {

            var main = ps.main;
            var count = this.CaptureBursts(ps.emission);
            this.sampledSeed = this.owner.randomSeed;
            var random = new System.Random(this.sampledSeed.HasValue == true ? unchecked((int)this.sampledSeed.Value)
                : ps.useAutoRandomSeed == true ? System.Environment.TickCount : unchecked((int)ps.randomSeed));
            this.capacity = Mathf.Min(main.maxParticles, 16000);
            this.initialParticles = new ParticleSystem.Particle[count];
            this.particles = new ParticleSystem.Particle[this.capacity];
            this.lifetimeTexture = new Texture2D(256, 2, TextureFormat.RGBAFloat, false, true) {
                name = "GPU particle lifetime", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            var maxSize = 0f;
            for (var i = 0; i < 256; i++) {
                var age = i / 255f;
                var color = ps.colorOverLifetime.enabled == true ? ps.colorOverLifetime.color.Evaluate(age) : Color.white;
                var sizeSample = ps.sizeOverLifetime.enabled == true ? ps.sizeOverLifetime.size.Evaluate(age) : 1f;
                maxSize = Mathf.Max(maxSize, Mathf.Abs(sizeSample));
                this.lifetimeTexture.SetPixel(i, 0, color);
                this.lifetimeTexture.SetPixel(i, 1, new Color(sizeSample, 0, 0, 0));
            }

            this.lifetimeTexture.Apply(false, true);
            this.material.SetTexture("_Lifetime", this.lifetimeTexture);
            var radius = ps.shape.enabled == true ? ps.shape.radius : 0f;
            var extent = radius;
            for (var i = 0; i < count; i++) {
                var lifetime = GpuParticleEmitter.Lifetime(ps, main.startLifetime.Evaluate(0, (float)random.NextDouble()), GpuParticleMotion.Random((uint)i + 1u));

                this.duration = Mathf.Max(this.duration, lifetime);
                GpuParticleEmitter.Shape(ps.shape, random, out var position, out var velocity);

                velocity *= main.startSpeed.Evaluate(0, (float)random.NextDouble());
                velocity += GpuParticleEmitter.InitialVelocity(ps, GpuParticleMotion.Random((uint)i + 1u));
                var size = GpuParticleEmitter.Size(main, (float)random.NextDouble());
                var rotation = main.startRotation.Evaluate(0, (float)random.NextDouble());
                var color = main.startColor.Evaluate(0, (float)random.NextDouble());

                this.motion.Evaluate(0f, lifetime, GpuParticleMotion.Random((uint)i + 1u), out _, out var moduleVelocity, out _, out var moduleRotation);
                this.initialParticles[i] = new ParticleSystem.Particle {
                    position = position,
                    velocity = velocity + moduleVelocity,
                    angularVelocity = (moduleRotation + this.appearance.RotationRate((velocity + moduleVelocity).magnitude, GpuParticleMotion.Random((uint)i + 1u))) * Mathf.Rad2Deg,
                    startLifetime = lifetime,
                    remainingLifetime = lifetime,
                    startSize3D = size,
                    rotation = rotation * Mathf.Rad2Deg,
                    startColor = color,
                    randomSeed = (uint)i + 1u,
                };

                extent = Mathf.Max(extent, position.magnitude + velocity.magnitude * lifetime
                    + .5f * Physics.gravity.magnitude * Mathf.Abs(main.gravityModifier.constant) * lifetime * lifetime + Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y)) * maxSize);
            }

            this.maxLifetimeSize = maxSize;
            this.bounds = new Bounds(Vector3.zero, Vector3.one * Mathf.Max(.1f, extent * 2));
            this.mesh = new Mesh { name = "Procedural particle seeds", hideFlags = HideFlags.HideAndDontSave, };
            this.mesh.bounds = this.bounds;

        }

        public void SetSimulationSpeed(float value) {

            // Rebase the clock without changing the last presented particle state.
            this.lastTime = ParticleEnvironment.Now(this.unscaled);
            this.speedMultiplier = value;

        }

        internal float prefabSimulationSpeed => this.speed;

        internal int availableEmissionCount => this.capacity - (int)this.ownParticleCount;

        internal void Emit(int count, List<Vector3> positions, ParticleEmitParams overrides = default, List<uint> seeds = null, bool rebuild = true) {

            this.AdvanceChildren();
            this.ReadDynamics();
            var survivors = 0;
            for (var i = 0; i < this.liveCount; i++) {
                var particle = this.EvaluateParticle(i);
                if (particle.remainingLifetime > 0f) {
                    this.particles[survivors++] = particle;
                } else {
                    this.trails?.Retain(particle, this.elapsed);
                }
            }

            this.liveCount = survivors;
            this.stateTime = this.elapsed;
            count = Mathf.Min(count, this.capacity - this.liveCount);
            for (var i = 0; i < count; i++) {
                var particle = this.initialParticles[(int)(this.emissionSequence % (uint)this.initialParticles.Length)];
                this.emissionSequence++;
                var oldSpeedRotation = this.appearance.RotationRate(particle.velocity.magnitude, GpuParticleMotion.Random(particle.randomSeed));
                this.motion.Evaluate(0f, particle.startLifetime, GpuParticleMotion.Random(particle.randomSeed), out _, out var oldVelocity, out _, out var oldRotation);
                particle.randomSeed = seeds != null ? seeds[i] : this.emissionSequence;
                this.motion.Evaluate(0f, particle.startLifetime, GpuParticleMotion.Random(particle.randomSeed), out _, out var newVelocity, out _, out var newRotation);
                particle.velocity += newVelocity - oldVelocity;
                particle.angularVelocity += (newRotation - oldRotation) * Mathf.Rad2Deg;
                if (positions != null) {
                    particle.position = positions[i];
                }

                overrides.Apply(ref particle);
                particle.angularVelocity += (this.appearance.RotationRate(particle.velocity.magnitude, GpuParticleMotion.Random(particle.randomSeed)) - oldSpeedRotation) * Mathf.Rad2Deg;
                this.particles[this.liveCount++] = particle;
                this.EmitBirth(particle);
            }

            if (rebuild == true) {
                this.UpdateMesh();
            }

        }

        public void SetSystemScale(Vector3 value) {

            this.instance.gameObject.transform.localScale = value;

        }

        public void Play() {

            if (this.sampledSeed != this.owner.randomSeed) {
                ParticleEnvironment.Destroy(this.mesh);
                ParticleEnvironment.Destroy(this.lifetimeTexture);
                this.BuildMesh(this.sourceSystem);
                this.instance.gameObject.GetComponent<MeshFilter>().sharedMesh = this.mesh;
            }
            this.trails?.Clear();
            this.ResetChildren();
            this.emissionSequence = 0;
            this.elapsed = 0;
            this.stateTime = 0;
            this.liveCount = 0;
            this.dynamics?.SetNoiseSeed(this.owner.randomSeed ?? this.sourceSystem.randomSeed);
            this.nextBurst = 0;
            this.automaticBursts = true;
            this.UpdateMesh();
            this.AdvanceSimulation(0f);

            this.lastTime = ParticleEnvironment.Now(this.unscaled);
            this.playing = true;
            this.material.SetFloat(GpuParticleBackend.elapsedId, 0);
            this.renderer.enabled = true;
            this.lights?.SetTime(this.elapsed);
            this.trails?.SetTime(0f);
            this.trails?.SetEnabled(true);
            this.lights?.SetEnabled(true);
            this.presenter?.Play();

        }

        public void Stop() {

            this.playing = false;
            this.automaticBursts = false;
            this.trails?.Clear();
            this.liveCount = 0;
            this.renderer.enabled = false;
            this.trails?.SetEnabled(false);
            this.lights?.SetEnabled(false);
            this.StopChildren();
            this.presenter?.Stop();

        }

        public void Pause() {

        }

        public void Resume() {

            this.lastTime = ParticleEnvironment.Now(this.unscaled);

        }

        public void StopEmitting() {

            // Birth/Death events of existing particles remain active.
            this.automaticBursts = false;

        }

        public void Tick() {

            if (this.owner.isPaused == false && this.owner.automaticSimulation == true) {
                var now = ParticleEnvironment.Now(this.unscaled);
                this.AdvanceSimulation(this.elapsed + (float)(now - this.lastTime) * this.speed * this.speedMultiplier);
                this.lastTime = now;
            }
            this.dynamics?.Step(this.elapsed);
            this.AdvanceChildren();
            this.material.SetFloat(GpuParticleBackend.elapsedId, this.elapsed);
            this.lights?.SetTime(this.elapsed);
            this.trails?.SetTime(this.elapsed);
            this.presenter?.PresentTextureFrame();

        }

        internal void Simulate(float deltaTime) {

            this.AdvanceSimulation(this.elapsed + deltaTime * this.speed * this.speedMultiplier);
            this.lights?.SetTime(this.elapsed);
            this.trails?.SetTime(this.elapsed);
            this.lastTime = ParticleEnvironment.Now(this.unscaled);
            this.material.SetFloat(GpuParticleBackend.elapsedId, this.elapsed);

        }

        internal int GetParticles(ParticleSystem.Particle[] buffer, int size, int offset) {

            this.ReadDynamics();
            var limit = size < 0 ? buffer.Length : size;
            var copied = 0;
            var skipped = 0;
            for (var i = 0; i < this.liveCount && copied < limit; i++) {
                var particle = this.EvaluateParticle(i);
                if (particle.remainingLifetime <= 0) {
                    continue;
                }

                if (skipped++ < offset) {
                    continue;
                }

                buffer[copied++] = particle;
            }

            return copied;

        }

        internal void SetParticles(ParticleSystem.Particle[] buffer, int size, int offset) {

            var count = size < 0 ? buffer.Length : size;
            if (offset > this.capacity || count > this.capacity - offset) {
                throw new System.ArgumentOutOfRangeException(nameof(size), "GPU particles exceed min(maxParticles, 16000).");
            }

            if ((uint)offset > this.ownParticleCount) {
                throw new System.ArgumentOutOfRangeException(nameof(offset));
            }

            this.AdvanceChildren();
            this.ReadDynamics();
            // Materialize all survivors at the last rendered simulation time before rebasing.
            var survivors = 0;
            for (var i = 0; i < this.liveCount; i++) {
                var particle = this.EvaluateParticle(i);
                if (particle.remainingLifetime > 0) {
                    this.particles[survivors++] = particle;
                } else {
                    this.trails?.Retain(particle, this.elapsed);
                }
            }

            this.liveCount = offset;
            for (var i = 0; i < count; i++) {
                if (buffer[i].remainingLifetime > 0) {
                    this.particles[this.liveCount++] = buffer[i];
                }
            }

            this.stateTime = this.elapsed;
            this.UpdateMesh();

        }

        private void ReadDynamics() {

            if (this.dynamics != null) {
                this.dynamics.Step(this.elapsed);
                this.dynamics.Read(this.particles);
                this.stateTime = this.elapsed;
            }

        }

        private ParticleSystem.Particle EvaluateParticle(int index) {

            var particle = this.particles[index];
            return this.EvaluateParticle(particle, this.elapsed - this.stateTime);

        }

        private ParticleSystem.Particle EvaluateParticle(ParticleSystem.Particle particle, float delta) {

            var lifetime = Mathf.Max(particle.startLifetime, .000001f);
            var age = (particle.startLifetime - particle.remainingLifetime) / lifetime;
            var random = GpuParticleMotion.Random(particle.randomSeed);
            this.motion.Evaluate(age, lifetime, random, out var p0, out var v0, out var r0, out var w0);
            this.motion.Evaluate(age + delta / lifetime, lifetime, random, out var p1, out var v1, out var r1, out var w1);
            var angularVelocity = particle.angularVelocity;
            if (this.appearance.rotationEnabled == true) {
                var baseRate = this.appearance.RotationRate(particle.velocity.magnitude, random);
                var angle = 0f;
                for (var sample = 0; sample < 8; sample++) {
                    var time = delta * ((sample + .5f) / 8f);
                    this.motion.Evaluate(age + time / lifetime, lifetime, random, out _, out var moduleVelocity, out _, out _);
                    angle += this.appearance.RotationRate((particle.velocity + this.gravity * time + moduleVelocity - v0).magnitude, random);
                }

                particle.rotation += (angle / 8f - baseRate) * delta * Mathf.Rad2Deg;
                particle.angularVelocity += (this.appearance.RotationRate((particle.velocity + this.gravity * delta + v1 - v0).magnitude, random) - baseRate) * Mathf.Rad2Deg;
            }

            particle.position += particle.velocity * delta + .5f * this.gravity * delta * delta + p1 - p0 - v0 * delta;
            particle.velocity += v1 - v0;
            particle.rotation += (r1 - r0 - w0 * delta) * Mathf.Rad2Deg;
            particle.angularVelocity += (w1 - w0) * Mathf.Rad2Deg;
            particle.velocity += this.gravity * delta;
            particle.rotation += angularVelocity * delta;
            particle.remainingLifetime -= delta;
            return particle;

        }

        private void UpdateMesh() {

            using var verticesLease = UnityEngine.Pool.ListPool<Vector3>.Get(out var vertices);
            using var colorsLease = UnityEngine.Pool.ListPool<Color32>.Get(out var colors);
            using var uvLease = UnityEngine.Pool.ListPool<Vector2>.Get(out var uv);
            using var motionLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var motion);
            using var sizesLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var sizes);
            using var idsLease = UnityEngine.Pool.ListPool<Vector2>.Get(out var ids);
            using var agesLease = UnityEngine.Pool.ListPool<Vector4>.Get(out var ages);
            using var indicesLease = UnityEngine.Pool.ListPool<int>.Get(out var indices);
            this.bounds = new Bounds(Vector3.zero, Vector3.one * .1f);
            this.duration = this.elapsed;
            for (var i = 0; i < this.liveCount; i++) {
                var particle = this.particles[i];
                var lifetime = particle.remainingLifetime;
                this.duration = Mathf.Max(this.duration, this.stateTime + lifetime + (this.trails?.TailLifetime(particle.startLifetime) ?? 0f));
                var motionHorizon = Mathf.Max(lifetime, this.trails?.HistoryLifetime(particle.startLifetime) ?? 0f);
                var extent = this.motion.BaseVelocity(particle).magnitude * motionHorizon + .5f * this.gravity.magnitude * motionHorizon * motionHorizon
                    + GpuParticleEmitter.Width(particle) * this.maxLifetimeSize * this.appearance.sizeBound + this.motion.BoundsPadding(particle.startLifetime) + (this.trails?.WidthPadding(GpuParticleEmitter.Width(particle)) ?? 0f);
                extent += this.appearance.lightRangeBound * (this.sourceSystem.lights.sizeAffectsRange == true ? GpuParticleEmitter.Width(particle) * this.maxLifetimeSize * this.appearance.sizeBound : 1f);
                extent += (this.dynamics?.accelerationBound ?? 0f) * .5f * motionHorizon * motionHorizon + (this.dynamics?.planeBound ?? 0f);
                this.bounds.Encapsulate(new Bounds(particle.position, Vector3.one * extent * 2));
                for (var corner = 0; corner < 4; corner++) {
                    ids.Add(new Vector2(i, 0f));
                    vertices.Add(particle.position);
                    colors.Add(particle.startColor);
                    uv.Add(new Vector2(corner == 1 || corner == 2 ? 1 : 0, corner >= 2 ? 1 : 0));
                    motion.Add(new Vector4(particle.velocity.x, particle.velocity.y, particle.velocity.z, lifetime));
                    sizes.Add(GpuParticleEmitter.SizeRotation(particle));
                    ages.Add(new Vector4(particle.startLifetime, particle.startLifetime - lifetime, particle.angularVelocity * Mathf.Deg2Rad, GpuParticleMotion.Random(particle.randomSeed)));
                }

                var first = i * 4;
                indices.Add(first);
                indices.Add(first + 1);
                indices.Add(first + 2);
                indices.Add(first);
                indices.Add(first + 2);
                indices.Add(first + 3);
            }

            if (this.trails != null) {
                this.trails.ExpandBounds(ref this.bounds, this.elapsed, this.motion, this.gravity);
                this.duration = Mathf.Max(this.duration, this.trails.endTime);
            }

            this.mesh.Clear();
            this.mesh.SetVertices(vertices);
            this.mesh.SetColors(colors);
            this.mesh.SetUVs(0, uv);
            this.mesh.SetUVs(1, motion);
            this.mesh.SetUVs(2, sizes);
            this.mesh.SetUVs(3, ages);
            this.mesh.SetUVs(5, ids);
            this.mesh.SetTriangles(indices, 0);
            this.mesh.bounds = this.bounds;
            this.material.SetFloat("_StateTime", this.stateTime);
            this.lights?.UpdateMesh(this.mesh, this.lifetimeTexture, this.stateTime);
            this.trails?.SetLifetime(this.lifetimeTexture);
            this.trails?.Rebuild(this.particles, this.liveCount, this.stateTime, this.bounds, this.motion, this.gravity);
            this.dynamics?.Upload(this.particles, this.liveCount, this.stateTime);
            this.ScheduleDeaths();

        }

        public void Draw(MeshGenerationContext context) {

        }

        public void Dispose() {

            this.playing = false;
            if (this.presenter != null) {
                this.presenter.Dispose();
                this.presenter = null;
            }

            this.DisposeChildren();
            this.trails?.Dispose();
            this.instance.Dispose();
            ParticleEnvironment.Destroy(this.mesh);
            ParticleEnvironment.Destroy(this.material);
            ParticleEnvironment.Destroy(this.lifetimeTexture);
            this.motion?.Dispose();
            this.appearance?.Dispose();
            this.dynamics?.Dispose();
            this.lights?.Dispose();

        }

    }

}
