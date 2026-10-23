using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    /// <summary>A temporary particle overlay that reveals an existing, laid-out element.</summary>
    public sealed class ParticleReveal : IDisposable {

        private static readonly Dictionary<VisualElement, ParticleReveal> active = new();
        private static readonly ConditionalWeakTable<VisualElement, HiddenState> hidden = new();
        private readonly bool hiding;
        private readonly StyleFloat shownOpacity;
        private readonly VisualElement target;
        private readonly RevealOverlay overlay;
        private readonly StyleFloat originalOpacity;
        private readonly float visibleOpacity;
        private readonly float duration;
        private readonly double started;
        private readonly IVisualElementScheduledItem ticker;
        private bool disposed;

        public bool isRunning => this.disposed == false;

        /// <summary>Start the mode selected by --meparticles-reveal-mode (cloud or assemble).</summary>
        public static ParticleReveal Play(VisualElement target, Texture snapshot = null) {

            var mode = ReadStyle(target, "mode", "cloud").Trim().ToLowerInvariant();
            return mode switch {
                "cloud" => Cloud(target),
                "assemble" => Assemble(target, snapshot),
                _ => throw new ArgumentException("Unknown --meparticles-reveal-mode: " + mode),
            };

        }

        /// <summary>Explicit arguments override USS; omitted arguments use USS, then defaults.</summary>
        public static ParticleReveal Cloud(VisualElement target, float? duration = null, uint? count = null, float? spread = null, Color? color = null, bool? hide = null) {

            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            var tint = color ?? (target.customStyle.TryGetValue(new CustomStyleProperty<Color>("--meparticles-reveal-color"), out var styledColor) == true
                ? styledColor : new Color(.3f, .85f, 1f, 1f));
            return new ParticleReveal(target, null, duration ?? ReadFloat(target, "duration", .8f),
                count ?? ReadCount(target, "count", 100u), spread ?? ReadFloat(target, "spread", 60f), tint, hide ?? ReadHide(target));

        }

        /// <summary>Assemble fragments. Caller owns snapshot. Explicit arguments override USS.</summary>
        public static ParticleReveal Assemble(VisualElement target, Texture snapshot, float? duration = null, uint? columns = null, float? spread = null, bool? hide = null) {

            if (snapshot == null) {
                throw new ArgumentNullException(nameof(snapshot));
            }

            return new ParticleReveal(target, snapshot, duration ?? ReadFloat(target, "duration", 1.2f),
                columns ?? ReadCount(target, "columns", 32u), spread ?? ReadFloat(target, "spread", 120f), Color.white, hide ?? ReadHide(target));

        }

        private static bool ReadHide(VisualElement target) {

            return ReadStyle(target, "direction", "show").Trim().ToLowerInvariant() switch {
                "show" => false,
                "hide" => true,
                var value => throw new ArgumentException("Unknown --meparticles-reveal-direction: " + value),
            };

        }

        private sealed class HiddenState {

            internal StyleFloat opacity;
            internal float visibleOpacity;

        }

        private static string ReadStyle(VisualElement target, string name, string fallback) {

            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            return target.customStyle.TryGetValue(new CustomStyleProperty<string>("--meparticles-reveal-" + name), out var value) == true ? value : fallback;

        }

        private static float ReadFloat(VisualElement target, string name, float fallback) {

            return float.Parse(ReadStyle(target, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

        }

        private static uint ReadCount(VisualElement target, string name, uint fallback) {

            return uint.Parse(ReadStyle(target, name, fallback.ToString(CultureInfo.InvariantCulture)), CultureInfo.InvariantCulture);

        }

        private ParticleReveal(VisualElement target, Texture snapshot, float duration, uint amount, float spread, Color color, bool hide) {

            if (target == null) {
                throw new ArgumentNullException(nameof(target));
            }

            if (target.panel == null || target.parent == null || Valid(target.layout.width) == false || Valid(target.layout.height) == false) {
                throw new InvalidOperationException("Reveal requires an attached element with a finite, positive layout.");
            }

            if (Valid(duration) == false || float.IsNaN(spread) == true || float.IsInfinity(spread) == true || spread < 0f) {
                throw new ArgumentOutOfRangeException(nameof(duration), "Duration must be positive and spread nonnegative; both must be finite.");
            }

            if (amount == 0u || amount > (snapshot == null ? 4096u : 128u)) {
                throw new ArgumentOutOfRangeException(nameof(amount));
            }

            if (active.TryGetValue(target, out var previous) == true) {
                previous.Dispose();
            }

            this.target = target;
            this.originalOpacity = target.style.opacity;
            this.hiding = hide;
            var wasHidden = hidden.TryGetValue(target, out var state);
            this.shownOpacity = wasHidden == true ? state.opacity : this.originalOpacity;
            this.visibleOpacity = wasHidden == true && hide == false ? state.visibleOpacity
                : previous != null ? previous.visibleOpacity : target.resolvedStyle.opacity;
            this.duration = duration;
            this.overlay = new RevealOverlay(target, snapshot, amount, spread, color);
            // A sibling is unaffected by target opacity and shares ancestor transforms and clipping.
            target.parent.Add(this.overlay);
            this.overlay.PlaceInFront(target);
            this.overlay.style.opacity = this.visibleOpacity;
            this.overlay.progress = hide == true && snapshot != null ? 1f : 0f;
            target.style.opacity = hide == true ? this.visibleOpacity : 0f;
            target.RegisterCallback<DetachFromPanelEvent>(this.OnDetach);
            this.overlay.RegisterCallback<DetachFromPanelEvent>(this.OnDetach);
            active.Add(target, this);
            this.started = Time.realtimeSinceStartupAsDouble;
            this.ticker = this.overlay.schedule.Execute(this.Tick).Every(16);

        }

        private static bool Valid(float value) {

            return float.IsNaN(value) == false && float.IsInfinity(value) == false && value > 0f;

        }

        private void Tick() {

            if (this.target.parent != this.overlay.parent || this.target.panel != this.overlay.panel) {
                this.Dispose();
                return;
            }

            var progress = Mathf.Clamp01((float)((Time.realtimeSinceStartupAsDouble - this.started) / this.duration));
            var presentation = this.hiding == true ? 1f - progress : progress;
            this.overlay.progress = this.overlay.assembled == true ? presentation : progress;
            var fade = this.overlay.assembled == true ? Mathf.InverseLerp(.8f, 1f, presentation)
                : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.1f, .75f, presentation));
            this.target.style.opacity = this.visibleOpacity * fade;
            this.overlay.MarkDirtyRepaint();
            if (progress >= 1f) {
                this.Finish(true);
            }

        }

        private void OnDetach(DetachFromPanelEvent evt) {

            this.Dispose();

        }

        /// <summary>Cancel or finish, restoring the original inline opacity and removing the overlay.</summary>
        public void Dispose() {

            this.Finish(false);

        }

        private void Finish(bool completed) {

            if (this.disposed == true) {
                return;
            }

            this.disposed = true;
            this.ticker?.Pause();
            this.target.UnregisterCallback<DetachFromPanelEvent>(this.OnDetach);
            this.overlay.UnregisterCallback<DetachFromPanelEvent>(this.OnDetach);
            if (completed == true && this.hiding == true) {
                if (hidden.TryGetValue(this.target, out _) == false) {
                    hidden.Add(this.target, new HiddenState {
                        opacity = this.shownOpacity,
                        visibleOpacity = this.visibleOpacity,
                    });
                }

                this.target.style.opacity = 0f;
            } else if (completed == true) {
                this.target.style.opacity = this.shownOpacity;
                hidden.Remove(this.target);
            } else {
                this.target.style.opacity = this.originalOpacity;
            }
            active.Remove(this.target);
            this.overlay.RemoveFromHierarchy();

        }

        private sealed class RevealOverlay : VisualElement {

            private readonly VisualElement target;
            private readonly Texture snapshot;
            private readonly Vector2[] offsets;
            private readonly Vector2[] centers;
            private readonly Color color;
            private readonly int columns;
            private readonly int rows;
            internal float progress;
            internal bool assembled => this.snapshot != null;

            internal RevealOverlay(VisualElement target, Texture snapshot, uint amount, float spread, Color color) {

                this.target = target;
                this.snapshot = snapshot;
                this.color = color;
                this.name = "particle-reveal-overlay";
                this.pickingMode = PickingMode.Ignore;
                this.style.position = Position.Absolute;
                this.style.left = 0;
                this.style.top = 0;
                this.style.width = 1;
                this.style.height = 1;
                this.style.overflow = Overflow.Visible;
                this.columns = (int)amount;
                this.rows = snapshot == null ? 1 : Mathf.Clamp(Mathf.CeilToInt(amount * target.layout.height / target.layout.width), 1, 128);
                var count = this.columns * this.rows;
                this.offsets = new Vector2[count];
                this.centers = new Vector2[count];
                var random = new System.Random(7421);
                for (var i = 0; i < count; i++) {
                    var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                    var distance = spread * (.3f + .7f * (float)random.NextDouble());
                    this.offsets[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
                    this.centers[i] = new Vector2((float)random.NextDouble(), (float)random.NextDouble());
                }

                this.generateVisualContent += this.Draw;

            }

            private void Draw(MeshGenerationContext context) {

                if (this.target.panel == null || this.target.panel != this.panel) {
                    return;
                }

                // At most 16384 cells; split to stay below UITK's 65535-vertex limit.
                for (var start = 0; start < this.offsets.Length; start += 8192) {
                    var count = Math.Min(8192, this.offsets.Length - start);
                    var mesh = context.Allocate(count * 4, count * 6, this.snapshot);
                    for (var j = 0; j < count; j++) {
                        this.DrawParticle(mesh, start + j, j);
                    }
                }

            }

            private void DrawParticle(MeshWriteData mesh, int index, int localIndex) {

                var rect = new Rect(Vector2.zero, this.target.layout.size);
                var p = this.progress;
                var assembly = this.assembled;
                var delay = this.centers[index].x * .18f;
                var t = Mathf.Clamp01((p - delay) / .62f);
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                var x = index % this.columns;
                var y = index / this.columns;
                var cell = new Vector2(rect.width / this.columns, rect.height / this.rows);
                var center = assembly == true ? new Vector2((x + .5f) * cell.x, (y + .5f) * cell.y)
                    : Vector2.Scale(this.centers[index], rect.size);
                center += this.offsets[index] * (assembly == true ? 1f - ease : p);
                var size = assembly == true ? cell * Mathf.Lerp(.2f, 1f, ease) : Vector2.one * (2f + this.centers[index].y * 4f) * (1f - p);
                var tint = this.color;
                tint.a *= assembly == true ? Mathf.InverseLerp(0f, .12f, p) * (1f - Mathf.InverseLerp(.8f, 1f, p)) : (1f - p) * Mathf.InverseLerp(0f, .08f, p);
                var uv = assembly == true ? new Rect((float)x / this.columns, 1f - (float)(y + 1) / this.rows, 1f / this.columns, 1f / this.rows) : new Rect(0, 0, 1, 1);
                this.Vertex(mesh, center + new Vector2(-size.x, -size.y) * .5f, new Vector2(uv.xMin, uv.yMax), tint);
                this.Vertex(mesh, center + new Vector2(size.x, -size.y) * .5f, new Vector2(uv.xMax, uv.yMax), tint);
                this.Vertex(mesh, center + new Vector2(size.x, size.y) * .5f, new Vector2(uv.xMax, uv.yMin), tint);
                this.Vertex(mesh, center + new Vector2(-size.x, size.y) * .5f, new Vector2(uv.xMin, uv.yMin), tint);
                var first = (ushort)(localIndex * 4);
                mesh.SetNextIndex(first);
                mesh.SetNextIndex((ushort)(first + 1));
                mesh.SetNextIndex((ushort)(first + 2));
                mesh.SetNextIndex((ushort)(first + 2));
                mesh.SetNextIndex((ushort)(first + 3));
                mesh.SetNextIndex(first);

            }

            private void Vertex(MeshWriteData mesh, Vector2 position, Vector2 uv, Color tint) {

                var local = this.target.ChangeCoordinatesTo(this, position);
                mesh.SetNextVertex(new Vertex {
                    position = new Vector3(local.x, local.y, UnityEngine.UIElements.Vertex.nearZ),
                    uv = uv,
                    tint = tint,
                });

            }

        }

    }

}