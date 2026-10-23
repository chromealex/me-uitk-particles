using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    [UxmlElement("meparticles")]
    public sealed partial class ParticleElement : VisualElement, System.IDisposable {

        public ParticleRenderMode activeMode { get; private set; }
        #if UNITY_EDITOR || DEBUG
        public string compatibilityInfo { get; private set; }
        #else
        public string compatibilityInfo { get; private set; } = "Turn on DEBUG define";
        #endif
        public float pixelsPerUnit { get; set; } = 65f;
        public float visualScale { get; private set; } = 1f;
        public Vector3 systemScale { get; private set; } = Vector3.one;
        internal float effectivePixelsPerUnit => this.pixelsPerUnit * this.visualScale;
        public bool useJobs { get; set; } = true;
        public uint jobVertexThreshold { get; set; } = 4096;
        public bool isPaused { get; private set; }
        private double pauseStartTime;
        public bool hasCompletedPlayback { get; private set; }
        public uint particleCount => this.backend != null ? this.backend.particleCount : 0;
        public uint bakedVertexCount => this.backend != null ? this.backend.bakedVertexCount : 0;
        public Bounds renderBounds => this.backend != null ? this.backend.renderBounds : default;
        public bool isAutoSelecting => this.backend is AutoParticleBackend automatic && automatic.isSelecting == true;
        public bool hasInstance => this.backend != null;
        /// <summary>True when the current backend exposes particle data.</summary>
        public bool supportsParticleAccess => this.backend is NativeParticleBackend || this.backend is GpuParticleBackend;
        /// <summary>Number of accessible systems, including inactive native children. GPU exposes one system.</summary>
        public uint particleSystemCount => this.backend is NativeParticleBackend native ? (uint)native.systems.Length : this.backend is GpuParticleBackend shader ? shader.systemCount : 0u;
        internal IParticleBackend backend { get; private set; }
        private Bounds configuredGpuBounds = new(Vector3.zero, Vector3.one * 10f);
        public Bounds gpuBounds {
            get => this.configuredGpuBounds;
            set {
                VfxGraphParticleBackend.ValidateBounds(value);
                this.configuredGpuBounds = value;
            }
        }

        public ParticleCompletionSettings completionSettings {
            get => this.configuredCompletion;
            set {
                value.Validate();
                this.configuredCompletion = value;
            }
        }

        private ParticleCompletionSettings configuredCompletion, playbackCompletion;
        private bool completionPending, observedParticles;
        private double playbackStartTime;
        private int playbackStartFrame;
        private double lastEditorTime;
        internal float editorDeltaTime { get; private set; }
        private GameObject prefab;
        private ParticleRenderMode requestedMode;
        private bool requestedPlay;
        private readonly IVisualElementScheduledItem ticker;

        public ParticleElement() {

            this.pickingMode = PickingMode.Ignore;
            this.AddToClassList("meparticles");
            this.RegisterCallback<CustomStyleResolvedEvent>(this.OnParticleCustomStyle);
            this.generateVisualContent += this.DrawParticles;
            this.ticker = this.schedule.Execute(this.Tick).Every(16);
            this.ticker.Pause();
            this.RegisterCallback<AttachToPanelEvent, ParticleElement>(static (_, obj) => {
                obj.UpdateOverlay();
                obj.EnsureInstance();
                if (obj.requestedPlay == true) {
                    obj.StartPlayback();
                }
            }, this);
            this.RegisterCallback<DetachFromPanelEvent, ParticleElement>(static (_, obj) => {
                obj.RemoveOverlay();
                obj.FinishPresentationTransition();
                obj.Release();
                ParticleEnvironment.trackEditorElement?.Invoke(obj, false);
            }, this);

        }

        /// <summary>Sets a positive uniform UI scale without changing simulation coordinates.</summary>
        public void SetVisualScale(float value) {

            if (float.IsFinite(value) == false || value <= 0f) {
                throw new System.ArgumentOutOfRangeException(nameof(value), "Visual scale must be finite and positive.");
            }

            this.visualScale = value;
            this.MarkDirtyRepaint();

        }

        /// <summary>
        /// Sets a positive XYZ multiplier relative to the prefab root scale, without restarting playback.
        /// Native ParticleSystems retain their configured scalingMode and simulation space.
        /// </summary>
        public void SetSystemScale(Vector3 value) {

            if (float.IsFinite(value.x) == false || float.IsFinite(value.y) == false || float.IsFinite(value.z) == false
                || value.x <= 0f || value.y <= 0f || value.z <= 0f) {
                throw new System.ArgumentOutOfRangeException(nameof(value), "System scale components must be finite and positive.");
            }

            this.systemScale = value;
            this.backend?.SetSystemScale(value);
            this.MarkDirtyRepaint();

        }

        public void SetPrefab(GameObject value, ParticleRenderMode mode = ParticleRenderMode.Auto) {

            ParticleElement.ValidateRenderMask(mode);
            if (value != null || mode == ParticleRenderMode.VFXGraph) {
                ParticleElement.SelectRenderMode(value, mode);
            }

            this.Release();
            this.ClearAttractor();
            this.UnbindEmissionArea();
            this.hasCompletedPlayback = false;
            this.prefab = value;
            this.requestedMode = mode;
            this.requestedPlay = false;
            if (this.panel != null) {
                this.EnsureInstance();
            }

        }

        public void Play() {

            this.CancelAsyncPlayback();
            this.StartPlayback();

        }

        private void StartPlayback() {

            this.emissionRemainder = 0d;
            this.managedEmissionStopped = false;
            this.ClearFlights();
            this.isPaused = false;

            this.requestedPlay = true;
            this.hasCompletedPlayback = false;
            this.completionPending = false;
            this.EnsureInstance();
            if (this.backend == null) {
                return;
            }

            this.lastEditorTime = ParticleEnvironment.Now(true);
            this.lastAttractorTime = ParticleEnvironment.Now(false);
            this.manualElapsed = 0;
            this.ApplyRandomSeed();
            this.backend.Play();
            if (this.automaticSimulation == false && this.backend is NativeParticleBackend) {
                this.backend.Pause();
            }

            this.playbackCompletion = this.configuredCompletion;
            this.playbackStartTime = this.playbackCompletion.clock == ParticleCompletionClock.Unscaled ? ParticleEnvironment.Now(true) : ParticleEnvironment.Now(false);
            this.playbackStartFrame = Time.frameCount;
            this.observedParticles = false;
            this.completionPending = true;
            this.ticker.Resume();

        }

        public void Stop() {

            this.ClearFlights();
            this.CancelAsyncPlayback();
            this.isPaused = false;

            this.requestedPlay = false;
            this.completionPending = false;
            this.ticker.Pause();
            if (this.backend != null) {
                this.backend.Stop();
            }

            this.MarkDirtyRepaint();

        }

        /// <summary>Freezes the current simulation and completion clock, retaining the visible particles.</summary>
        public void Pause() {

            if (this.backend == null || this.requestedPlay == false || this.isPaused == true) {
                return;
            }

            this.backend.Pause();
            this.pauseStartTime = ParticleEnvironment.Now(this.playbackCompletion.clock == ParticleCompletionClock.Unscaled);
            this.isPaused = true;

        }

        /// <summary>Continues a paused run without clearing particles or sending a new play event.</summary>
        public void Resume() {

            if (this.backend == null || this.isPaused == false) {
                return;
            }

            this.playbackStartTime += ParticleEnvironment.Now(this.playbackCompletion.clock == ParticleCompletionClock.Unscaled) - this.pauseStartTime;
            this.lastEditorTime = ParticleEnvironment.Now(true);
            if (this.automaticSimulation == true || this.backend is GpuParticleBackend) {
                this.backend.Resume();
            }
            this.lastAttractorTime = ParticleEnvironment.Now(false);
            this.isPaused = false;
            this.ResetFlightClocks();

        }

        /// <summary>
        /// Ends future emission without clearing live particles or cancelling completion.
        /// GPU has no future emission after Play. VFXGraph sends OnStop and requires the graph to handle it.
        /// </summary>
        public void StopEmitting() {

            if (this.backend == null || this.requestedPlay == false) {
                return;
            }

            this.managedEmissionStopped = true;
            this.emissionRemainder = 0d;
            this.backend.StopEmitting();

        }

        /// <summary>
        /// Converts a simulation-space position to this element's local UI coordinates.
        /// X/Y are UI units; Z retains effect depth in UI units for a reversible 3D mapping.
        /// VFXGraph is unsupported because its graphs may contain different simulation spaces.
        /// </summary>
        public Vector3 SimulationToLocal(Vector3 position, int systemIndex = 0) {

            return this.SimulationToLocalMatrix(systemIndex).MultiplyPoint3x4(position);

        }

        /// <summary>Inverse of SimulationToLocal. Pass its Z to preserve depth; Z=0 selects the effect plane.</summary>
        public Vector3 LocalToSimulation(Vector3 position, int systemIndex = 0) {

            var matrix = this.SimulationToLocalMatrix(systemIndex);
            if (float.IsFinite(matrix.determinant) == false || Mathf.Abs(matrix.determinant) < 1e-12f) {
                throw new System.InvalidOperationException("The simulation-to-UI transform is not invertible.");
            }

            return matrix.inverse.MultiplyPoint3x4(position);

        }

        private Matrix4x4 SimulationToLocalMatrix(int systemIndex) {

            Matrix4x4 simulation;
            if (this.backend is GpuParticleBackend) {
                this.ValidateGpuSystem(systemIndex);
                simulation = Matrix4x4.Scale(this.systemScale);
            } else {
                this.GetNativeSystem(systemIndex);
                simulation = ((NativeParticleBackend)this.backend).SimulationToEffectMatrix(systemIndex);
            }

            var density = this.effectivePixelsPerUnit;
            if (float.IsFinite(density) == false || density <= 0f) {
                throw new System.InvalidOperationException("Coordinate conversion requires a finite positive pixelsPerUnit and visualScale product.");
            }

            var center = this.presentationOrigin;
            if (float.IsFinite(center.x) == false || float.IsFinite(center.y) == false) {
                throw new System.InvalidOperationException("Coordinate conversion requires resolved element layout. Wait for GeometryChangedEvent.");
            }

            return Matrix4x4.TRS(center, Quaternion.identity, new Vector3(density, -density, density)) * simulation;

        }

        /// <summary>
        /// Copies live particles from one system into a reusable buffer and returns the copied count.
        /// Position and velocity use that system's simulation space. Size -1 uses the whole buffer;
        /// offset selects the first particle in the system, not in the buffer.
        /// </summary>
        /// <exception cref="System.NotSupportedException">The active backend is VFXGraph (VFX Graph).</exception>
        public int GetParticles(ParticleSystem.Particle[] particles, int size = -1, int offset = 0, int systemIndex = 0) {

            ParticleElement.ValidateParticleBuffer(particles, size, offset);
            if (this.backend is GpuParticleBackend shader) {
                this.ValidateGpuSystem(systemIndex);
                return shader.GetSystem(systemIndex).GetParticles(particles, size, offset);
            }

            return this.GetNativeSystem(systemIndex).GetParticles(particles, size, offset);

        }

        /// <summary>
        /// Writes particles to one system using Unity's SetParticles semantics.
        /// Pass the count returned by GetParticles to avoid writing unused buffer entries.
        /// Negative remainingLifetime removes a particle. Playback is not restarted.
        /// </summary>
        /// <exception cref="System.NotSupportedException">The active backend is VFXGraph (VFX Graph).</exception>
        public void SetParticles(ParticleSystem.Particle[] particles, int size = -1, int offset = 0, int systemIndex = 0) {

            ParticleElement.ValidateParticleBuffer(particles, size, offset);
            if (this.backend is GpuParticleBackend shader) {
                this.ValidateGpuSystem(systemIndex);
                shader.GetSystem(systemIndex).SetParticles(particles, size, offset);
            } else {
                this.GetNativeSystem(systemIndex).SetParticles(particles, size, offset);
            }
            this.MarkDirtyRepaint();

        }

        /// <summary>Returns the live count for one system, for sizing a reusable particle buffer.</summary>
        /// <exception cref="System.NotSupportedException">The active backend is VFXGraph (VFX Graph).</exception>
        public uint GetParticleCount(int systemIndex = 0) {

            if (this.backend is GpuParticleBackend shader) {
                this.ValidateGpuSystem(systemIndex);
                return shader.GetSystem(systemIndex).ownParticleCount;
            }

            return (uint)this.GetNativeSystem(systemIndex).particleCount;

        }

        private void ValidateGpuSystem(int systemIndex) {

            if (systemIndex < 0 || (uint)systemIndex >= this.particleSystemCount) {
                throw new System.ArgumentOutOfRangeException(nameof(systemIndex));
            }

        }

        private ParticleSystem GetNativeSystem(int systemIndex) {

            if (this.backend == null) {
                throw new System.InvalidOperationException("Particle access requires an attached element with an initialized backend in Play Mode.");
            }

            if (this.backend is not NativeParticleBackend native) {
                throw new System.NotSupportedException("Particle access requires Mesh, RenderTexture, RenderTextureAtlas or GPU. VFXGraph does not expose particles.");
            }

            if (systemIndex < 0 || systemIndex >= native.systems.Length) {
                throw new System.ArgumentOutOfRangeException(nameof(systemIndex));
            }

            var system = native.systems[systemIndex];
            if (system == null) {
                throw new System.InvalidOperationException("The selected ParticleSystem has been destroyed.");
            }

            return system;

        }

        private static void ValidateParticleBuffer(ParticleSystem.Particle[] particles, int size, int offset) {

            if (particles == null) {
                throw new System.ArgumentNullException(nameof(particles));
            }

            if (size < -1 || size > particles.Length) {
                throw new System.ArgumentOutOfRangeException(nameof(size));
            }

            if (offset < 0) {
                throw new System.ArgumentOutOfRangeException(nameof(offset));
            }

        }

        public static MeshCompatibilityResult MeshCompatibility(GameObject source) {

            return MeshParticleCompatibility.MeshCompatibility(source);

        }

        /// <summary>Null means compatible; otherwise returns the reason Auto falls back.</summary>
        public static string GpuCompatibility(GameObject source) {

            return GpuParticleCompatibility.Check(source);

        }

        private static void ValidateRenderMask(ParticleRenderMode modes) {

            if (modes == ParticleRenderMode.None || (modes & ~(ParticleRenderMode.Auto | ParticleRenderMode.VFXGraph)) != 0
                || (modes & ParticleRenderMode.VFXGraph) != 0 && modes != ParticleRenderMode.VFXGraph) {
                throw new System.ArgumentOutOfRangeException(nameof(modes), "Use a nonempty ParticleSystem mode mask, or VFXGraph by itself.");
            }

        }

        private static ParticleRenderMode SelectRenderMode(GameObject source, ParticleRenderMode modes) {

            if (modes == ParticleRenderMode.VFXGraph) {
                VfxGraphParticleBackend.ValidatePrefab(source);
                return ParticleRenderMode.VFXGraph;
            }

            if (source == null || source.GetComponentInChildren<ParticleSystem>(true) == null) {
                throw new System.ArgumentException("Prefab must contain a ParticleSystem.", nameof(source));
            }

            var shaderReason = (modes & ParticleRenderMode.GPU) != 0 ? ParticleElement.GpuCompatibility(source) : "excluded";
            if (shaderReason == null) {
                return ParticleRenderMode.GPU;
            }

            var meshReason = (modes & ParticleRenderMode.Mesh) != 0 ? ParticleElement.MeshCompatibility(source) : (MeshCompatibilityResult?)null;
            if (meshReason == MeshCompatibilityResult.Compatible) {
                return ParticleRenderMode.Mesh;
            }

            if ((modes & ParticleRenderMode.RenderTexture) != 0) {
                return ParticleRenderMode.RenderTexture;
            }

            var atlasReason = (modes & ParticleRenderMode.RenderTextureAtlas) != 0 ? AtlasParticleCompatibility.Check(source) : "excluded";
            if (atlasReason == null) {
                return ParticleRenderMode.RenderTextureAtlas;
            }

            var meshDetails = meshReason.HasValue == true ? MeshParticleCompatibility.DescribeCompatibility(meshReason.Value) : "excluded";
            throw new System.ArgumentException($"No compatible mode in {modes}. GPU: {shaderReason}; Mesh: {meshDetails}; Atlas: {atlasReason}.", nameof(source));

        }

        private void EnsureInstance() {

            if (this.panel != null && this.prefab != null) {
                ParticleEnvironment.trackEditorElement?.Invoke(this, true);
            }

            if (this.backend != null || this.prefab == null || this.panel == null || ParticleEnvironment.editorSuspended == true
                || (Application.isPlaying == false && this.panel.contextType != ContextType.Editor)) {
                return;
            }

            this.activeMode = ParticleElement.SelectRenderMode(this.prefab, this.requestedMode);
            var allowTexture = (this.requestedMode & ParticleRenderMode.RenderTexture) != 0;
            var allowAtlas = (this.requestedMode & ParticleRenderMode.RenderTextureAtlas) != 0;
            var atlasCandidate = this.activeMode == ParticleRenderMode.RenderTextureAtlas
                || this.activeMode == ParticleRenderMode.RenderTexture && allowAtlas == true && AtlasParticleCompatibility.Check(this.prefab) == null;
            #if UNITY_EDITOR || DEBUG
            this.compatibilityInfo = $"Allowed: {this.requestedMode}; selected: {this.activeMode}";
            if ((this.requestedMode & ParticleRenderMode.GPU) != 0) {
                if (this.activeMode == ParticleRenderMode.GPU) {
                    var system = this.prefab.GetComponent<ParticleSystem>();
                    this.compatibilityInfo += GpuParticleDynamics.SpatialNoise(system) == true
                        ? "; GPU spatial curl Noise"
                        : system.noise.enabled == true ? "; analytic simulation, approximate wave Noise" : "; analytic simulation";
                } else {
                    this.compatibilityInfo += "; GPU fallback: " + ParticleElement.GpuCompatibility(this.prefab);
                }
            }
            #endif

            this.backend = atlasCandidate == true ? new AtlasParticleBackend(this, allowTexture, allowTexture) : this.activeMode switch {
                ParticleRenderMode.GPU => new GpuParticleBackend(this),
                ParticleRenderMode.VFXGraph => new VfxGraphParticleBackend(this),
                ParticleRenderMode.Mesh => allowTexture == true && Application.isPlaying == true ? new AutoParticleBackend(this, allowAtlas) : new MeshParticleBackend(this),
                _ => new RenderTextureParticleBackend(this),
            };
            try {
                if (this.backend is VfxGraphParticleBackend && this.automaticSimulation == false) {
                    throw new System.NotSupportedException("VFXGraph does not support manual simulation.");
                }
                this.backend.Initialize(this.prefab);
            } catch {
                this.backend.Dispose();
                this.backend = null;
                throw;
            }

        }

        internal void SetAutoMode(ParticleRenderMode mode, string details) {

            this.activeMode = mode;
            #if UNITY_EDITOR || DEBUG
            this.compatibilityInfo = details;
            #endif

        }

        internal void SetAtlasMode(bool active) {

            this.activeMode = active == true ? ParticleRenderMode.RenderTextureAtlas : ParticleRenderMode.RenderTexture;
            #if UNITY_EDITOR || DEBUG
            this.compatibilityInfo = active == true ? "Atlas: shared camera and texture" : "RenderTexture: atlas grouping or size fallback";
            #endif

        }

        private void DrawParticles(MeshGenerationContext context) {

            if (this.backend != null && this.renderOnTop == false) {
                this.backend.Draw(context);
            }

        }

        private void Tick() {

            if (this.backend == null || this.requestedPlay == false) {
                return;
            }

            if (Application.isPlaying == false) {
                var nowEditor = ParticleEnvironment.Now(true);
                this.editorDeltaTime = (float)System.Math.Clamp(nowEditor - this.lastEditorTime, 0, .1);
                this.lastEditorTime = nowEditor;
                if (this.isPaused == false && this.automaticSimulation == true && this.backend is NativeParticleBackend native) {
                    native.SimulateEditor(this.editorDeltaTime);
                }
            }

            this.backend.Tick();
            var attractionTime = ParticleEnvironment.Now(false);
            if (this.isPaused == false && this.automaticSimulation == true) {
                var delta = (float)(attractionTime - this.lastAttractorTime);
                var version = this.flightVersion;
                this.UpdateFlights(delta, false);
                if (version != this.flightVersion) {
                    return;
                }
                this.UpdateAttractor(delta);
                if (version != this.flightVersion) {
                    return;
                }
                this.UpdateManagedEmission(delta);
            }
            this.lastAttractorTime = attractionTime;

            if (this.automaticSimulation == true && this.isPaused == false && this.completionPending == true && (Application.isPlaying == false || Time.frameCount > this.playbackStartFrame)) {
                var now = this.playbackCompletion.clock == ParticleCompletionClock.Unscaled ? ParticleEnvironment.Now(true) : ParticleEnvironment.Now(false);
                this.CheckCompletion(now - this.playbackStartTime);
            }

        }

        private void CheckCompletion(double elapsed) {

            if (this.isPaused == true || this.completionPending == false || this.requestedPlay == false || this.backend == null) {
                return;
            }

            var settings = this.playbackCompletion;
            var count = settings.countEnabled == true || this.backend.requiresObservedParticles == true ? this.particleCount : 0;
            this.observedParticles |= count > 0;
            var naturallyFinished = false;
            if ((settings.timeEnabled == false && settings.countEnabled == false) || (settings.countEnabled == true && this.observedParticles == false)) {
                naturallyFinished = this.isAlive == false
                    && (this.backend.requiresObservedParticles == false || this.observedParticles == true);
            }

            bool finished;
            if (settings.timeEnabled == false && settings.countEnabled == false) {
                finished = naturallyFinished;
            } else {
                var timePassed = elapsed > settings.timeGreaterThan;
                var countPassed = count < settings.countLessThan && (this.observedParticles == true || naturallyFinished == true);
                if (settings.match == ParticleCompletionMatch.All) {
                    finished = (settings.timeEnabled == false || timePassed == true) && (settings.countEnabled == false || countPassed == true);
                } else {
                    finished = (settings.timeEnabled == true && timePassed == true) || (settings.countEnabled == true && countPassed == true);
                }
            }

            if (finished == false) {
                return;
            }

            // Mark before invoking: subscribers may restart, replace, detach or dispose this element.
            this.completionPending = false;
            this.hasCompletedPlayback = true;
            var completedTask = this.asyncPlayback;
            this.asyncPlayback = null;
            if (settings.stopOnCompletion == true) {
                this.Stop();
            }

            completedTask?.TrySetResult(true);
            using (var evt = ParticleCompletedEvent.GetPooled()) {
                evt.target = this;
                this.SendEvent(evt);
            }

        }

        internal void Release() {

            this.ClearFlights();
            this.CancelAsyncPlayback();
            this.isPaused = false;
            this.completionPending = false;
            this.ticker.Pause();
            if (this.backend != null) {
                this.backend.Dispose();
                this.backend = null;
            }

        }

        internal void RestoreEditorPlayback() {

            if (this.panel == null) {
                return;
            }

            this.EnsureInstance();
            if (this.requestedPlay == true) {
                this.Play();
            }

        }

        public void Dispose() {

            this.SetRenderOnTop(false);

            this.UnbindEmissionArea();
            this.ClearAttractor();
            this.FinishPresentationTransition();
            this.markupUpdate?.Pause();
            this.markupEnabled = false;
            this.markupApplied = false;

            this.requestedPlay = false;
            this.prefab = null;
            this.Release();
            ParticleEnvironment.trackEditorElement?.Invoke(this, false);

        }

    }

}
