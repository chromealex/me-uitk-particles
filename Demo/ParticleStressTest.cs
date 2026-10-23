// <summary>
// Runs configurable particle stress workloads and measures frame time and whole-frame GC
// while comparing rendering modes, mesh jobs and transparent overdraw.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.VFX;

namespace ME.UITK.Particles.Demo {

    [RequireComponent(typeof(UIDocument))]
    public sealed class ParticleStressTest : MonoBehaviour {

        public GameObject particlePrefab;
        [UnityEngine.Serialization.FormerlySerializedAs("gpuPrefab")]
        public GameObject vfxGraphPrefab;
        public StyleSheet styleSheet;
        public VisualTreeAsset layout;
        public VisualTreeAsset cellTemplate;
        public readonly List<ParticleElement> effects = new();
        public string lastMeasurement { get; private set; }
        public uint targetCount { get; private set; } = 1000;

        private readonly List<GameObject> sources = new();
        private VisualElement root, stage;
        private Label status, result;
        private DropdownField countPicker, instancePicker, modePicker;
        private Toggle overdraw, liveCounters, jobs;
        private ProfilerRecorder gcRecorder;
        private float nextStatus, warmupEnd, sampleElapsed, sampleMax;
        private uint sampleFrames;
        private long sampleGc, peakGc;
        private bool measuring;
        private uint configuredInstances = 1;
        private ParticleRenderMode configuredMode = ParticleRenderMode.Mesh;

        private void OnEnable() {

            this.root = this.GetComponent<UIDocument>().rootVisualElement;
            this.root.Clear();
            if (this.styleSheet != null && this.root.styleSheets.Contains(this.styleSheet) == false) {
                this.root.styleSheets.Add(this.styleSheet);
            }

            this.layout.CloneTree(this.root);
            this.root.AddToClassList("stress-root");
            this.countPicker = this.root.Q<DropdownField>("particle-count");
            this.countPicker.choices = new List<string> { "1000", "5000", "10000", "25000", "50000", };
            this.countPicker.index = 0;
            this.instancePicker = this.root.Q<DropdownField>("instance-count");
            this.instancePicker.choices = new List<string> { "1", "4", "16", };
            this.instancePicker.index = 0;
            this.modePicker = this.root.Q<DropdownField>("render-mode");
            this.modePicker.choices = new List<string> { "Auto", "Mesh", "RenderTexture", "RenderTextureAtlas", "VFXGraph", };
            this.modePicker.index = 0;
            this.overdraw = this.root.Q<Toggle>("overdraw");
            this.jobs = this.root.Q<Toggle>("jobs");
            this.liveCounters = this.root.Q<Toggle>("live-counters");
            this.root.Q<Button>("apply").clicked += this.ApplySelection;
            this.root.Q<Button>("stop").clicked += this.Stop;
            this.root.Q<Button>("measure").clicked += this.Measure;
            this.root.Q<Button>("atlas-four").clicked += () => this.Configure(10000u, 4u, ParticleRenderMode.RenderTextureAtlas);
            this.root.Q<Button>("atlas-sixteen").clicked += () => this.Configure(10000u, 16u, ParticleRenderMode.RenderTextureAtlas);
            this.status = this.root.Q<Label>("stress-status");
            this.result = this.root.Q<Label>("measurement-result");
            this.stage = this.root.Q("stress-stage");
            this.stage.RegisterCallback<GeometryChangedEvent>(_ => this.ResizeEffects());
            this.gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
            this.root.schedule.Execute(this.ApplySelection);

        }

        private void ApplySelection() {

            this.Configure(uint.Parse(this.countPicker.value), uint.Parse(this.instancePicker.value),
                System.Enum.Parse<ParticleRenderMode>(this.modePicker.value));

        }

        public void Configure(uint total, uint instances, ParticleRenderMode mode) {

            this.measuring = false;
            this.ReleaseEffects();
            this.targetCount = System.Math.Min(System.Math.Max(total, 1u), 50000u);
            this.configuredInstances = System.Math.Min(System.Math.Max(instances, 1u), 16u);
            this.configuredMode = mode;
            this.countPicker.SetValueWithoutNotify(this.targetCount.ToString());
            this.instancePicker.SetValueWithoutNotify(this.configuredInstances.ToString());
            this.modePicker.SetValueWithoutNotify(mode.ToString());
            this.jobs.SetEnabled((mode & ParticleRenderMode.Mesh) != 0);
            var prefab = mode == ParticleRenderMode.VFXGraph ? this.vfxGraphPrefab : this.particlePrefab;
            if (prefab == null) {
                this.status.text = mode == ParticleRenderMode.VFXGraph ? "Assign the GPU stress prefab." : "Assign the stress particle prefab.";
                return;
            }

            var columns = Mathf.CeilToInt(Mathf.Sqrt(this.configuredInstances));
            var rows = Mathf.CeilToInt((float)this.configuredInstances / columns);
            for (var i = 0u; i < this.configuredInstances; i++) {
                var source = Object.Instantiate(prefab);
                source.name = "[Stress template] " + i;
                source.hideFlags = HideFlags.DontSave;
                source.transform.position = Vector3.one * -20000;
                var count = this.targetCount / this.configuredInstances + (i < this.targetCount % this.configuredInstances ? 1u : 0u);
                if (mode == ParticleRenderMode.VFXGraph) {
                    var visualEffect = source.GetComponent<VisualEffect>();
                    visualEffect.SetFloat("ParticleCount", count);
                    visualEffect.SetFloat("SizeMin", this.overdraw.value == true ? .2f : .035f);
                    visualEffect.SetFloat("SizeMax", this.overdraw.value == true ? .45f : .075f);
                    visualEffect.enabled = false;
                } else {
                    var system = source.GetComponent<ParticleSystem>();
                    system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    var main = system.main;
                    main.maxParticles = (int)System.Math.Max(1u, count);
                    main.startSize = this.overdraw.value == true ? new ParticleSystem.MinMaxCurve(.2f, .45f) : new ParticleSystem.MinMaxCurve(.035f, .075f);
                    var emission = system.emission;
                    emission.SetBursts(new[] { new ParticleSystem.Burst(0, new ParticleSystem.MinMaxCurve(count)), });
                    source.GetComponent<ParticleSystemRenderer>().enabled = false;
                }

                this.sources.Add(source);
                var cell = this.cellTemplate.Instantiate().Q(className: "stress-cell");
                cell.style.width = Length.Percent(100f / columns);
                cell.style.height = Length.Percent(100f / rows);
                var effect = cell.Q<ParticleElement>();
                effect.uxmlUseJobs = this.jobs.value;
                effect.uxmlCompletionTimeEnabled = mode == ParticleRenderMode.VFXGraph;
                effect.uxmlPixelsPerUnit = Mathf.Max(1, Mathf.Min(this.stage.contentRect.width / columns / 15f, this.stage.contentRect.height / rows / 9f));
                effect.uxmlRenderMode = mode;
                effect.uxmlPrefab = source;
                this.stage.Add(cell);
                effect.ApplyUxmlSettings();
                this.effects.Add(effect);
                effect.Play();
            }

            this.nextStatus = 0;
            this.result.text = "Workload applied. Use Measure for an average over 10 seconds, without live HUD allocations.";

        }

        private void ResizeEffects() {

            var columns = Mathf.CeilToInt(Mathf.Sqrt(this.configuredInstances));
            var rows = Mathf.CeilToInt((float)this.configuredInstances / columns);
            var density = Mathf.Max(1, Mathf.Min(this.stage.contentRect.width / columns / 15f, this.stage.contentRect.height / rows / 9f));
            foreach (var effect in this.effects) {
                effect.uxmlPixelsPerUnit = density;
                effect.ApplyUxmlSettings();
            }

        }

        public void Stop() {

            this.measuring = false;
            foreach (var effect in this.effects) {
                effect.Stop();
            }

            this.status.text = "Stopped. Apply / restart to emit again.";

        }

        public void Measure() {

            if (this.effects.Count == 0) {
                return;
            }

            foreach (var effect in this.effects) {
                effect.Play();
            }

            this.measuring = true;
            this.warmupEnd = Time.unscaledTime + 2;
            this.sampleElapsed = this.sampleMax = 0;
            this.sampleFrames = 0;
            this.sampleGc = this.peakGc = 0;
            this.result.text = "Measuring: 2 seconds warm-up + 10 seconds sampling. Keep the Game View focused; HUD is frozen.";

        }

        private void Update() {

            if (this.measuring == true) {
                foreach (var effect in this.effects) {
                    if (effect.isAutoSelecting == true) {
                        this.warmupEnd = Time.unscaledTime + .5f;
                        this.sampleElapsed = this.sampleMax = 0f;
                        this.sampleFrames = 0u;
                        this.sampleGc = this.peakGc = 0;
                        return;
                    }
                }

                if (Time.unscaledTime < this.warmupEnd) {
                    return;
                }

                var dt = Time.unscaledDeltaTime;
                this.sampleElapsed += dt;
                this.sampleMax = Mathf.Max(this.sampleMax, dt);
                this.sampleFrames++;
                if (this.gcRecorder.Valid == true) {
                    var bytes = this.gcRecorder.LastValue;
                    this.sampleGc += bytes;
                    this.peakGc = System.Math.Max(this.peakGc, bytes);
                }

                if (this.sampleElapsed >= 10) {
                    this.measuring = false;
                    var gc = this.gcRecorder.Valid == true ? $"GC/frame avg {this.sampleGc / this.sampleFrames:N0} B, peak {this.peakGc:N0} B" : "GC counter unavailable";
                    this.lastMeasurement = $"{this.targetCount:N0} particles, {this.configuredInstances} elements, {this.configuredMode}: "
                        + $"{this.sampleFrames / this.sampleElapsed:F1} FPS, avg {1000 * this.sampleElapsed / this.sampleFrames:F2} ms, worst {1000 * this.sampleMax:F2} ms. {gc}.";
                    this.result.text = this.lastMeasurement;
                    Debug.Log(this.lastMeasurement);
                }

                return;
            }

            if (this.liveCounters.value == false || Time.unscaledTime < this.nextStatus) {
                return;
            }

            this.nextStatus = Time.unscaledTime + .5f;
            var count = 0u;
            var vertices = 0u;
            foreach (var effect in this.effects) {
                count += effect.particleCount;
                vertices += effect.bakedVertexCount;
            }

            this.status.text = $"{(this.configuredMode == ParticleRenderMode.VFXGraph ? "Alive (GPU delayed)" : "Alive")} {count:N0} / {this.targetCount:N0}    Elements {this.effects.Count}    {this.configuredMode} -> {(this.effects.Count > 0 ? this.effects[0].activeMode : this.configuredMode)}    UI vertices {vertices:N0}";

        }

        private void ReleaseEffects() {

            foreach (var effect in this.effects) {
                effect.Dispose();
            }

            this.effects.Clear();
            this.stage?.Clear();
            foreach (var source in this.sources) {
                Object.Destroy(source);
            }

            this.sources.Clear();

        }

        private void OnDisable() {

            this.measuring = false;
            this.ReleaseEffects();
            this.gcRecorder.Dispose();
            this.root?.Clear();

        }

    }

}
