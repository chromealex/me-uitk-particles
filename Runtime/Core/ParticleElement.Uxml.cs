using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        // Keep declarative defaults separate so removing a USS rule restores the UXML value.
        private bool markupEnabled;
        private IVisualElementScheduledItem markupUpdate;
        private bool markupApplied;
        private bool appliedAutoPlay;

        private GameObject markupPrefab = null;
        [UxmlAttribute("prefab")]
        public GameObject uxmlPrefab {
            get => this.markupPrefab;
            set {
                this.markupPrefab = value;
                this.QueueMarkupUpdate();
            }
        }

        private ParticleRenderMode markupRenderMode = ParticleRenderMode.Auto;
        [UxmlAttribute("render-mode")]
        public ParticleRenderMode uxmlRenderMode {
            get => this.markupRenderMode;
            set {
                this.markupRenderMode = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupAutoPlay = true;
        [UxmlAttribute("auto-play")]
        public bool uxmlAutoPlay {
            get => this.markupAutoPlay;
            set {
                this.markupAutoPlay = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupPixelsPerUnit = 65f;
        [UxmlAttribute("pixels-per-unit")]
        public float uxmlPixelsPerUnit {
            get => this.markupPixelsPerUnit;
            set {
                this.markupPixelsPerUnit = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupUseJobs = true;
        [UxmlAttribute("use-jobs")]
        public bool uxmlUseJobs {
            get => this.markupUseJobs;
            set {
                this.markupUseJobs = value;
                this.QueueMarkupUpdate();
            }
        }

        private uint markupJobVertexThreshold = 4096u;
        [UxmlAttribute("job-vertex-threshold")]
        public uint uxmlJobVertexThreshold {
            get => this.markupJobVertexThreshold;
            set {
                this.markupJobVertexThreshold = value;
                this.QueueMarkupUpdate();
            }
        }

        private Vector3 markupGpuBoundsCenter = Vector3.zero;
        [UxmlAttribute("gpu-bounds-center")]
        public Vector3 uxmlGpuBoundsCenter {
            get => this.markupGpuBoundsCenter;
            set {
                this.markupGpuBoundsCenter = value;
                this.QueueMarkupUpdate();
            }
        }

        private Vector3 markupGpuBoundsSize = Vector3.one * 10f;
        [UxmlAttribute("gpu-bounds-size")]
        public Vector3 uxmlGpuBoundsSize {
            get => this.markupGpuBoundsSize;
            set {
                this.markupGpuBoundsSize = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupCompletionTimeEnabled = false;
        [UxmlAttribute("completion-time-enabled")]
        public bool uxmlCompletionTimeEnabled {
            get => this.markupCompletionTimeEnabled;
            set {
                this.markupCompletionTimeEnabled = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupCompletionTimeGreaterThan = 0f;
        [UxmlAttribute("completion-time-greater-than")]
        public float uxmlCompletionTimeGreaterThan {
            get => this.markupCompletionTimeGreaterThan;
            set {
                this.markupCompletionTimeGreaterThan = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupCompletionCountEnabled = false;
        [UxmlAttribute("completion-count-enabled")]
        public bool uxmlCompletionCountEnabled {
            get => this.markupCompletionCountEnabled;
            set {
                this.markupCompletionCountEnabled = value;
                this.QueueMarkupUpdate();
            }
        }

        private uint markupCompletionCountLessThan = 1u;
        [UxmlAttribute("completion-count-less-than")]
        public uint uxmlCompletionCountLessThan {
            get => this.markupCompletionCountLessThan;
            set {
                this.markupCompletionCountLessThan = value;
                this.QueueMarkupUpdate();
            }
        }

        private ParticleCompletionMatch markupCompletionMatch = ParticleCompletionMatch.Any;
        [UxmlAttribute("completion-match")]
        public ParticleCompletionMatch uxmlCompletionMatch {
            get => this.markupCompletionMatch;
            set {
                this.markupCompletionMatch = value;
                this.QueueMarkupUpdate();
            }
        }

        private ParticleCompletionClock markupCompletionClock = ParticleCompletionClock.Unscaled;
        [UxmlAttribute("completion-clock")]
        public ParticleCompletionClock uxmlCompletionClock {
            get => this.markupCompletionClock;
            set {
                this.markupCompletionClock = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupStopOnCompletion = false;
        [UxmlAttribute("stop-on-completion")]
        public bool uxmlStopOnCompletion {
            get => this.markupStopOnCompletion;
            set {
                this.markupStopOnCompletion = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupEmissionRate = 0f;
        [UxmlAttribute("emission-rate")]
        public float uxmlEmissionRate {
            get => this.markupEmissionRate;
            set {
                this.markupEmissionRate = value;
                this.QueueMarkupUpdate();
            }
        }

        private int markupEmissionSystemIndex = 0;
        [UxmlAttribute("emission-system-index")]
        public int uxmlEmissionSystemIndex {
            get => this.markupEmissionSystemIndex;
            set {
                this.markupEmissionSystemIndex = value;
                this.QueueMarkupUpdate();
            }
        }

        private string markupEmissionArea = string.Empty;
        [UxmlAttribute("emission-area")]
        public string uxmlEmissionArea {
            get => this.markupEmissionArea;
            set {
                this.markupEmissionArea = value;
                this.QueueMarkupUpdate();
            }
        }

        private Color markupTint = Color.white;
        [UxmlAttribute("tint")]
        public Color uxmlTint {
            get => this.markupTint;
            set {
                this.markupTint = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupVisualScale = 1f;
        [UxmlAttribute("visual-scale")]
        public float uxmlVisualScale {
            get => this.markupVisualScale;
            set {
                this.markupVisualScale = value;
                this.QueueMarkupUpdate();
            }
        }

        private Vector3 markupSystemScale = Vector3.one;
        [UxmlAttribute("system-scale")]
        public Vector3 uxmlSystemScale {
            get => this.markupSystemScale;
            set {
                this.markupSystemScale = value;
                this.QueueMarkupUpdate();
            }
        }

        private Vector2 markupPosition = Vector2.zero;
        [UxmlAttribute("position")]
        public Vector2 uxmlPosition {
            get => this.markupPosition;
            set {
                this.markupPosition = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupSimulationSpeed = 1f;
        [UxmlAttribute("simulation-speed")]
        public float uxmlSimulationSpeed {
            get => this.markupSimulationSpeed;
            set {
                this.markupSimulationSpeed = value;
                this.QueueMarkupUpdate();
            }
        }

        private string markupAttractorTarget = string.Empty;
        [UxmlAttribute("attractor-target")]
        public string uxmlAttractorTarget {
            get => this.markupAttractorTarget;
            set {
                this.markupAttractorTarget = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupAttractorStrength = 0f;
        [UxmlAttribute("attractor-strength")]
        public float uxmlAttractorStrength {
            get => this.markupAttractorStrength;
            set {
                this.markupAttractorStrength = value;
                this.QueueMarkupUpdate();
            }
        }

        private float markupAttractorRadius = 0f;
        [UxmlAttribute("attractor-radius")]
        public float uxmlAttractorRadius {
            get => this.markupAttractorRadius;
            set {
                this.markupAttractorRadius = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupAttractorKillOnArrival = false;
        [UxmlAttribute("attractor-kill-on-arrival")]
        public bool uxmlAttractorKillOnArrival {
            get => this.markupAttractorKillOnArrival;
            set {
                this.markupAttractorKillOnArrival = value;
                this.QueueMarkupUpdate();
            }
        }

        private int markupAttractorSystemIndex = 0;
        [UxmlAttribute("attractor-system-index")]
        public int uxmlAttractorSystemIndex {
            get => this.markupAttractorSystemIndex;
            set {
                this.markupAttractorSystemIndex = value;
                this.QueueMarkupUpdate();
            }
        }

        private string markupRandomSeed = string.Empty;
        [UxmlAttribute("random-seed")]
        public string uxmlRandomSeed {
            get => this.markupRandomSeed;
            set {
                this.markupRandomSeed = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupAutomaticSimulation = true;
        [UxmlAttribute("automatic-simulation")]
        public bool uxmlAutomaticSimulation {
            get => this.markupAutomaticSimulation;
            set {
                this.markupAutomaticSimulation = value;
                this.QueueMarkupUpdate();
            }
        }

        private bool markupRenderOnTop;
        [UxmlAttribute("render-on-top")]
        public bool uxmlRenderOnTop {
            get => this.markupRenderOnTop;
            set {
                this.markupRenderOnTop = value;
                this.QueueMarkupUpdate();
            }
        }

        // Batch attribute setters: generated UXML deserialization order must not create a backend
        // or validate a partially configured completion condition.
        private void QueueMarkupUpdate() {

            this.markupEnabled = true;
            if (this.markupUpdate == null) {
                this.markupUpdate = this.schedule.Execute(this.ApplyMarkup);
            } else {
                this.markupUpdate.ExecuteLater(0);
            }

        }

        private void OnParticleCustomStyle(CustomStyleResolvedEvent evt) {

            if (evt.target != this) {
                return;
            }

            if (this.markupEnabled == true) {
                this.QueueMarkupUpdate();
            }

        }

        /// <summary>Applies pending UXML defaults and current USS values before an immediate Play call.</summary>
        public void ApplyUxmlSettings() {

            if (this.markupEnabled == false) {
                return;
            }

            this.markupUpdate?.Pause();
            this.ApplyMarkup();

        }

        private void ApplyMarkup() {

            try {
                var prefab = this.markupPrefab;
                var renderMode = this.ReadParticleStyle("render-mode", this.markupRenderMode);
                var autoPlay = this.ReadParticleStyle("auto-play", this.markupAutoPlay);
                var pixelsPerUnit = this.ReadParticleStyle("pixels-per-unit", this.markupPixelsPerUnit);
                var useJobs = this.ReadParticleStyle("use-jobs", this.markupUseJobs);
                var jobVertexThreshold = this.ReadParticleStyle("job-vertex-threshold", this.markupJobVertexThreshold);
                var gpuBoundsCenter = this.ReadVectorStyle("gpu-bounds-center", this.markupGpuBoundsCenter);
                var gpuBoundsSize = this.ReadVectorStyle("gpu-bounds-size", this.markupGpuBoundsSize);
                var completionTimeEnabled = this.ReadParticleStyle("completion-time-enabled", this.markupCompletionTimeEnabled);
                var completionTimeGreaterThan = this.ReadParticleStyle("completion-time-greater-than", this.markupCompletionTimeGreaterThan);
                var completionCountEnabled = this.ReadParticleStyle("completion-count-enabled", this.markupCompletionCountEnabled);
                var completionCountLessThan = this.ReadParticleStyle("completion-count-less-than", this.markupCompletionCountLessThan);
                var completionMatch = this.ReadParticleStyle("completion-match", this.markupCompletionMatch);
                var completionClock = this.ReadParticleStyle("completion-clock", this.markupCompletionClock);
                var stopOnCompletion = this.ReadParticleStyle("stop-on-completion", this.markupStopOnCompletion);
                if (float.IsNaN(pixelsPerUnit) == true || float.IsInfinity(pixelsPerUnit) == true || pixelsPerUnit <= 0f) {
                    throw new ArgumentOutOfRangeException(nameof(pixelsPerUnit));
                }

                var emissionRate = this.ReadParticleStyle("emission-rate", this.markupEmissionRate);
                var emissionIndex = this.ReadParticleStyle("emission-system-index", this.markupEmissionSystemIndex);
                var emissionArea = this.ReadParticleStyle("emission-area", this.markupEmissionArea)?.Trim() ?? string.Empty;
                if (float.IsFinite(emissionRate) == false || emissionRate < 0f || emissionIndex < 0) {
                    throw new ArgumentOutOfRangeException("emission-rate / emission-system-index");
                }
                if (renderMode == ParticleRenderMode.VFXGraph && (emissionRate != 0f || emissionIndex != 0 || emissionArea.Length > 0)) {
                    throw new NotSupportedException("VFXGraph does not support declarative managed emission.");
                }

                var visualScale = this.ReadParticleStyle("visual-scale", this.markupVisualScale);
                var systemScale = this.ReadVectorStyle("system-scale", this.markupSystemScale);
                var position = new Vector2(this.ReadParticleStyle("position-x", this.markupPosition.x),
                    this.ReadParticleStyle("position-y", this.markupPosition.y));
                var speed = this.ReadParticleStyle("simulation-speed", this.markupSimulationSpeed);
                var tint = this.customStyle.TryGetValue(new CustomStyleProperty<Color>("--meparticles-tint"), out var styleTint)
                    ? styleTint : this.markupTint;
                var attractor = this.ReadParticleStyle("attractor-target", this.markupAttractorTarget)?.Trim() ?? string.Empty;
                var strength = this.ReadParticleStyle("attractor-strength", this.markupAttractorStrength);
                var radius = this.ReadParticleStyle("attractor-radius", this.markupAttractorRadius);
                var kills = this.ReadParticleStyle("attractor-kill-on-arrival", this.markupAttractorKillOnArrival);
                var attractorIndex = this.ReadParticleStyle("attractor-system-index", this.markupAttractorSystemIndex);
                uint? seed = string.IsNullOrWhiteSpace(this.markupRandomSeed) == true ? null
                    : uint.Parse(this.markupRandomSeed, CultureInfo.InvariantCulture);
                if (seed == 0u || float.IsFinite(visualScale) == false || visualScale <= 0f
                    || float.IsFinite(systemScale.x) == false || systemScale.x <= 0f
                    || float.IsFinite(systemScale.y) == false || systemScale.y <= 0f
                    || float.IsFinite(systemScale.z) == false || systemScale.z <= 0f
                    || float.IsFinite(position.x) == false || float.IsFinite(position.y) == false
                    || float.IsFinite(speed) == false || speed < 0f
                    || float.IsFinite(strength) == false || strength < 0f
                    || float.IsFinite(radius) == false || radius < 0f || attractorIndex < 0) {
                    throw new ArgumentOutOfRangeException("Particle control attributes");
                }
                for (var channel = 0; channel < 4; channel++) {
                    if (float.IsFinite(tint[channel]) == false || tint[channel] < 0f || tint[channel] > 1f) {
                        throw new ArgumentOutOfRangeException("tint");
                    }
                }
                if (renderMode == ParticleRenderMode.VFXGraph && (attractor.Length > 0 || strength != 0f || radius != 0f
                    || kills == true || attractorIndex != 0 || seed.HasValue == true || this.markupAutomaticSimulation == false)) {
                    throw new NotSupportedException("VFXGraph does not support attractors, deterministic seeds or manual simulation.");
                }

                var bounds = new Bounds(gpuBoundsCenter, gpuBoundsSize);
                VfxGraphParticleBackend.ValidateBounds(bounds);
                var completion = new ParticleCompletionSettings {
                    timeEnabled = completionTimeEnabled,
                    timeGreaterThan = completionTimeGreaterThan,
                    countEnabled = completionCountEnabled,
                    countLessThan = completionCountLessThan,
                    match = completionMatch,
                    clock = completionClock,
                    stopOnCompletion = stopOnCompletion,
                };
                completion.Validate();

                var duration = this.ReadTransitionTime("transition-duration");
                var delay = this.ReadTransitionTime("transition-delay");
                var easing = this.ReadParticleStyle("transition-timing-function", "linear");
                if (easing != "linear" && easing != "ease-in" && easing != "ease-out" && easing != "ease-in-out") {
                    throw new ArgumentException("Unsupported meparticles transition timing function: " + easing);
                }

                var replace = this.prefab != prefab || this.requestedMode != renderMode;
                var wasPlaying = this.requestedPlay;
                this.SetPresentationTarget(pixelsPerUnit, bounds, duration, delay, easing, replace, visualScale, systemScale, position, tint, speed);
                if (replace == true) {
                    this.SetPrefab(prefab, renderMode);
                }

                if (renderMode != ParticleRenderMode.VFXGraph) {
                    if (this.emissionRate != emissionRate || this.emissionSystemIndex != emissionIndex) {
                        this.SetEmissionRate(emissionRate, emissionIndex);
                    }
                    if (this.declarativeEmissionArea != emissionArea) {
                        this.UnbindEmissionArea();
                        this.declarativeEmissionArea = emissionArea;
                    }
                    this.ResolveEmissionArea();
                }

                this.automaticSimulation = this.markupAutomaticSimulation;
                this.randomSeed = seed;
                if (this.backend != null && renderMode != ParticleRenderMode.VFXGraph) {
                    this.GetParticleCount(attractorIndex);
                }
                this.declarativeAttractor = attractor;
                this.attractorTarget = attractor.Length == 0 ? null : this.panel?.visualTree.Q(attractor);
                this.attractorStrength = strength;
                this.attractorRadius = radius;
                this.attractorKills = kills;
                this.attractorSystemIndex = attractorIndex;

                this.SetRenderOnTop(this.ReadParticleStyle("render-on-top", this.markupRenderOnTop));
                this.useJobs = useJobs;
                this.jobVertexThreshold = jobVertexThreshold;
                this.completionSettings = completion;
                if (autoPlay == true && (this.markupApplied == false || this.appliedAutoPlay == false || replace == true && wasPlaying == true)) {
                    this.Play();
                } else if (autoPlay == false && this.markupApplied == true && this.appliedAutoPlay == true) {
                    this.Stop();
                }

                this.appliedAutoPlay = autoPlay;
                this.markupApplied = true;
                this.MarkDirtyRepaint();
            } catch (Exception exception) {
                Debug.LogError("meparticles: " + exception.Message);
            }

        }

        private T ReadParticleStyle<T>(string name, T fallback) {

            if (this.customStyle.TryGetValue(new CustomStyleProperty<string>("--meparticles-" + name), out var text) == false) {
                return fallback;
            }

            text = text.Trim().Trim('"', '\'');
            if (typeof(T).IsEnum == true) {
                var parsed = (T)Enum.Parse(typeof(T), text, true);
                if (Enum.IsDefined(typeof(T), parsed) == false) {
                    throw new ArgumentException("Invalid " + name + ": " + text);
                }

                return parsed;
            }

            return (T)Convert.ChangeType(text, typeof(T), CultureInfo.InvariantCulture);

        }

        private Vector3 ReadVectorStyle(string name, Vector3 fallback) {

            return new Vector3(
                this.ReadParticleStyle(name + "-x", fallback.x),
                this.ReadParticleStyle(name + "-y", fallback.y),
                this.ReadParticleStyle(name + "-z", fallback.z));

        }

    }

}
