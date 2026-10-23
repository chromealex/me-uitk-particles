// <summary>
// Builds the interactive particle playground for overlay and world-space UI,
// including prefab selection, clipping and drawing behind or above controls.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles.Demo {

    [RequireComponent(typeof(UIDocument))]
    public sealed class ParticleDemo : MonoBehaviour {

        public GameObject[] prefabs;
        [UnityEngine.Serialization.FormerlySerializedAs("gpuPrefab")]
        public GameObject vfxGraphPrefab;
        public bool worldSpace;
        public StyleSheet styleSheet;
        public VisualTreeAsset layout;
        public VisualTreeAsset clickTemplate;
        public readonly List<ParticleElement> effects = new();
        private readonly List<ParticleElement> clickEffects = new();
        private readonly List<ParticleRenderMode> modes = new();
        private readonly List<Label> statuses = new();
        private readonly List<Button> emitButtons = new();
        private readonly List<string> unavailableReasons = new();
        private readonly List<uint?> displayedCounts = new();
        private readonly List<string> displayedInfo = new();
        private float nextStatusUpdate;
        private readonly List<VisualElement> viewports = new();
        private readonly List<Button> occluders = new();
        private readonly List<Label> crosshairs = new();
        private Label scenarioHint;
        private int scenario;
        private VisualElement root;
        private int selected;
        private bool loop;
        private Toggle repeat;
        private float nextBurst;
        private VisualElement panel;
        private ParticleDemo worldDemo;
        private Button tab2D;
        private Button tab3D;
        private bool showingWorld;

        private void Start() {

            if (this.worldSpace == true) {
                return;
            }

            foreach (var demo in Object.FindObjectsByType<ParticleDemo>(FindObjectsInactive.Include)) {
                if (demo.worldSpace == true && demo.gameObject.scene == this.gameObject.scene) {
                    this.worldDemo = demo;
                    break;
                }
            }

            this.tab3D.SetEnabled(this.worldDemo != null);
            if (this.worldDemo != null) {
                this.worldDemo.gameObject.SetActive(false);
            }

        }

        public void ShowWorld(bool value) {

            if (this.worldSpace == true || this.showingWorld == value || this.worldDemo == null) {
                return;
            }

            var source = this.showingWorld == true ? this.worldDemo : this;
            var preset = source.selected;
            var scenarioIndex = source.scenario;
            var repeating = source.loop;
            this.showingWorld = value;
            if (value == true) {
                this.loop = false;
                this.panel.RemoveFromHierarchy();
                this.worldDemo.gameObject.SetActive(true);
            } else {
                this.worldDemo.gameObject.SetActive(false);
                this.root.Add(this.panel);
            }

            var target = value == true ? this.worldDemo : this;
            target.Select(preset);
            target.SetScenario(scenarioIndex);
            target.loop = repeating;
            target.repeat.SetValueWithoutNotify(repeating);
            target.nextBurst = Time.unscaledTime + 2.8f;
            target.PlayAll();
            this.tab2D.EnableInClassList("selected", value == false);
            this.tab3D.EnableInClassList("selected", value == true);

        }

        private ParticleRevealDemo revealDemo;

        private void OnEnable() {

            this.root = this.GetComponent<UIDocument>().rootVisualElement;
            this.root.Clear();
            this.root.pickingMode = PickingMode.Ignore;
            this.root.RegisterCallback<ClickEvent>(this.OnButtonClick, TrickleDown.TrickleDown);
            this.layout.CloneTree(this.root);
            if (this.styleSheet != null && this.root.styleSheets.Contains(this.styleSheet) == false) {
                this.root.styleSheets.Add(this.styleSheet);
            }

            this.root.AddToClassList(this.worldSpace == true ? "world-root" : "overlay-root");
            this.panel = this.root.Q("demo-panel");
            this.tab2D = this.root.Q<Button>("tab-2d");
            this.tab3D = this.root.Q<Button>("tab-3d");
            this.tab2D.clicked += () => this.ShowWorld(false);
            this.tab3D.clicked += () => this.ShowWorld(true);
            if (this.worldSpace == true) {
                this.root.Q<Label>("eyebrow").text = "02 / WORLD SPACE";
                this.root.Q<Label>("title").text = "Particles in the world";
                this.root.Q<Label>("subtitle").text = "World-space UI, click the buttons";
            }

            var picker = this.root.Q("preset-picker");
            for (var i = 0; i < this.prefabs.Length; i++) {
                var index = i;
                var button = new Button(() => this.Select(index)) { text = this.prefabs[i].name, };
                button.AddToClassList("preset");
                picker.Add(button);
            }

            var scenarioIndex = 0;
            this.root.Query<Button>(className: "scenario").ForEach(button => {
                var choice = scenarioIndex++;
                button.clicked += () => {
                    this.SetScenario(choice);
                    this.PlayAll();
                };
            });
            this.scenarioHint = this.root.Q<Label>(className: "scenario-hint");
            this.root.Query(className: "card").ForEach(this.BindCard);
            this.root.Q<Button>("emit-all").clicked += this.PlayAll;
            this.root.Q<Button>("clear").clicked += () => {
                this.loop = false;
                this.repeat.SetValueWithoutNotify(false);
                foreach (var effect in this.effects) {
                    effect.Stop();
                }
            };
            this.repeat = this.root.Q<Toggle>("repeat");
            this.repeat.RegisterValueChangedCallback(evt => {
                this.loop = evt.newValue;
                this.nextBurst = 0;
            });
            this.root.Q<Toggle>("animate").RegisterValueChangedCallback(evt => {
                this.panel.EnableInClassList("animated", evt.newValue);
                this.PlayAll();
            });
            this.Select(0);
            this.SetScenario(0);
            this.revealDemo = new ParticleRevealDemo(this.root.Q("demo-sidebar"), this.GetComponent<UIDocument>().panelSettings);

        }

        private void OnButtonClick(ClickEvent evt) {

            if (evt.button != 0) {
                return;
            }

            var target = evt.target as VisualElement;
            var button = target as Button ?? target?.GetFirstAncestorOfType<Button>();
            if (button == null || button.enabledInHierarchy == false || button.GetFirstAncestorOfType<Scroller>() != null) {
                return;
            }

            var position = this.root.WorldToLocal(evt.position);
            var effect = this.clickTemplate.Instantiate().Q<ParticleElement>();
            effect.style.left = position.x - 80f;
            effect.style.top = position.y - 80f;
            effect.RegisterCallback<ParticleCompletedEvent>(this.OnClickEffectCompleted);
            this.clickEffects.Add(effect);
            this.root.Add(effect);

        }

        private void OnClickEffectCompleted(ParticleCompletedEvent evt) {

            var effect = evt.target as ParticleElement;
            if (effect == null || this.clickEffects.Remove(effect) == false) {
                return;
            }

            effect.UnregisterCallback<ParticleCompletedEvent>(this.OnClickEffectCompleted);
            effect.Dispose();
            effect.RemoveFromHierarchy();

        }

        private void BindCard(VisualElement card) {

            var effect = card.Q<ParticleElement>();
            var mode = effect.uxmlRenderMode;
            if (mode == ParticleRenderMode.VFXGraph && this.vfxGraphPrefab == null) {
                card.AddToClassList("hidden");
                return;
            }

            this.effects.Add(effect);
            this.modes.Add(mode);
            this.viewports.Add(card.Q(className: "viewport"));
            this.crosshairs.Add(card.Q<Label>(className: "crosshair"));
            var occluder = card.Q<Button>("occluding-button");
            var clicks = 0u;
            occluder.clicked += () => occluder.text = "UI clicks: " + (++clicks);
            this.occluders.Add(occluder);
            this.statuses.Add(card.Q<Label>(className: "status"));
            this.displayedCounts.Add(null);
            this.displayedInfo.Add(null);
            var emit = card.Q<Button>("emit-here");
            emit.clicked += effect.Play;
            this.emitButtons.Add(emit);
            this.unavailableReasons.Add(null);

        }

        // UITK paints siblings in hierarchy order. No camera sorting or particle Z offset is needed.
        public void SetScenario(int value) {

            this.scenario = Mathf.Clamp(value, 0, 2);
            this.panel.EnableInClassList("unclipped", this.scenario != 0);
            for (var i = 0; i < this.effects.Count; i++) {
                if (this.scenario == 1) {
                    this.effects[i].PlaceBehind(this.occluders[i]);
                } else {
                    this.effects[i].BringToFront();
                }
            }

            this.scenarioHint.text = this.scenario == 0 ? "Clipped to the preview bounds."
                : this.scenario == 1 ? "Particles behind an opaque button. Its center hides them."
                : "Particles above the button and text. The button still receives clicks.";
            var n = 0;
            this.root.Query<Button>(className: "scenario").ForEach(button => button.EnableInClassList("selected", n++ == this.scenario));

        }

        public void Select(int index) {

            if (this.prefabs == null || this.prefabs.Length == 0) {
                return;
            }

            this.selected = Mathf.Clamp(index, 0, this.prefabs.Length - 1);
            for (var i = 0; i < this.effects.Count; i++) {
                var mode = this.modes[i];
                var prefab = mode == ParticleRenderMode.VFXGraph ? this.vfxGraphPrefab : this.prefabs[this.selected];
                var reason = mode == ParticleRenderMode.GPU ? ParticleElement.GpuCompatibility(prefab) : null;
                if (mode == ParticleRenderMode.Mesh) {
                    var compatibility = ParticleElement.MeshCompatibility(prefab);
                    reason = compatibility != MeshCompatibilityResult.Compatible ? compatibility.ToString() : null;
                }

                this.unavailableReasons[i] = reason;
                this.emitButtons[i].SetEnabled(reason == null);
                this.effects[i].uxmlPrefab = reason == null ? prefab : null;
                this.effects[i].ApplyUxmlSettings();
                this.displayedCounts[i] = null;
                this.displayedInfo[i] = null;
                this.statuses[i].text = reason != null ? mode + " unavailable: " + reason : "Ready";
            }

            var n = 0;
            this.root.Query<Button>(className: "preset").ForEach(button => button.EnableInClassList("selected", n++ == this.selected));

        }

        public void PlayAll() {

            if (this.showingWorld == true) {
                return;
            }

            foreach (var e in this.effects) {
                e.Play();
            }

        }

        private void Update() {

            if (this.showingWorld == true) {
                return;
            }

            if (this.loop == true && Time.unscaledTime >= this.nextBurst) {
                this.PlayAll();
                this.nextBurst = Time.unscaledTime + 2.8f;
            }

            if (Time.unscaledTime < this.nextStatusUpdate) {
                return;
            }

            this.nextStatusUpdate = Time.unscaledTime + .1f;
            for (var i = 0; i < this.effects.Count; i++) {
                if (this.unavailableReasons[i] != null) {
                    continue;
                }

                var count = this.effects[i].particleCount;
                var info = this.effects[i].compatibilityInfo;
                if (this.modes[i] == ParticleRenderMode.Auto) {
                    var selection = this.effects[i].hasInstance == true
                        ? "Auto selected: " + this.effects[i].activeMode
                        : "Auto: waiting for selection";
                    info = selection + "\n" + info;
                }

                if (count == this.displayedCounts[i] && info == this.displayedInfo[i]) {
                    continue;
                }

                this.displayedCounts[i] = count;
                this.displayedInfo[i] = info;
                this.statuses[i].text = info + "\n" + count + " particles";
            }

        }

        private void OnDisable() {

            this.revealDemo?.Dispose();
            this.revealDemo = null;

            this.loop = false;
            this.showingWorld = false;
            if (this.worldDemo != null) {
                this.worldDemo.gameObject.SetActive(false);
            }

            foreach (var e in this.effects) {
                e.Dispose();
            }

            foreach (var effect in this.clickEffects) {
                effect.UnregisterCallback<ParticleCompletedEvent>(this.OnClickEffectCompleted);
                effect.Dispose();
            }

            this.clickEffects.Clear();
            this.root?.UnregisterCallback<ClickEvent>(this.OnButtonClick, TrickleDown.TrickleDown);
            this.effects.Clear();
            this.modes.Clear();
            this.statuses.Clear();
            this.emitButtons.Clear();
            this.unavailableReasons.Clear();
            this.displayedCounts.Clear();
            this.displayedInfo.Clear();
            this.nextStatusUpdate = 0;
            this.viewports.Clear();
            this.occluders.Clear();
            this.crosshairs.Clear();
            this.root?.Clear();

        }

    }

}