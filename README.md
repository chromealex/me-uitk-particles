<div align="center">

<img src="docs/media/logo.png" alt="ME.UITK.Particles" width="960" />

# ME.UITK.Particles

**English** | [Русский](README.ru.md)

**Your ParticleSystem prefabs. Inside UI Toolkit.**

Native Unity simulation | Mesh, RT, GPU & VFX Graph rendering | Overlay & world-space UI

**Zero managed allocations in measured steady-state rendering paths.**

[Measurement scope and exceptions](#performance--allocations)

![Unity 6000.6](https://img.shields.io/badge/Unity-6000.6-222c3c?logo=unity&logoColor=white)
![URP 17.6](https://img.shields.io/badge/URP-17.6-222c3c)
![UI Toolkit](https://img.shields.io/badge/UI-Toolkit-61dfc1?labelColor=222c3c&color=61dfc1)

[Quick start](#quick-start) | [API](#api-reference) | [Rendering](#rendering-paths) | [Demo](#playground) | [Русский](README.ru.md)

![Confetti rendered in screen overlay and a world-space UIDocument](docs/media/overview.gif)

*Real Unity Game View capture. Switches from the 2D overlay to the 3D world-space UI.*

</div>

## UXML and USS

`ParticleElement` is registered as `meparticles` in UI Builder. Use the default namespace
`ME.UITK.Particles` for `<meparticles />`, or declare `xmlns:me="ME.UITK.Particles"` and use
`<me:meparticles />`. Save the CSS below as `Effects.uss` next to the UXML.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns="ME.UITK.Particles">
    <ui:Style src="Effects.uss" />
    <meparticles name="reward" class="reward-particles"
        prefab="project://database/Assets/ME.UITK.Particles/Demo/Generated/Confetti.prefab"
        render-mode="Auto" auto-play="true" pixels-per-unit="65"
        completion-time-enabled="true" completion-time-greater-than="2"
        stop-on-completion="true" />
</ui:UXML>
```

```css
.reward-particles {
    width: 300px;
    height: 220px;
    overflow: hidden;
    --meparticles-pixels-per-unit: 80;
    --meparticles-use-jobs: true;
}

.reward-particles.compact {
    --meparticles-pixels-per-unit: 42;
}
```

The following declarative settings are available as UXML attributes:

| Attribute | Type / values | Default |
| --- | --- | --- |
| `prefab` | GameObject asset | null |
| `render-mode` | `Auto`, `Mesh`, `RenderTexture`, `RenderTextureAtlas`, `GPU`, `VFXGraph` | `Auto` |
| `auto-play` | bool | `true` |
| `pixels-per-unit` | float > 0 | `65` |
| `use-jobs` | bool | `true` |
| `job-vertex-threshold` | uint | `4096` |
| `gpu-bounds-center` | Vector3 | `0,0,0` |
| `gpu-bounds-size` | Vector3, positive | `10,10,10` |
| `completion-time-enabled` | bool | `false` |
| `completion-time-greater-than` | float >= 0 | `0` |
| `completion-count-enabled` | bool | `false` |
| `completion-count-less-than` | uint >= 1 when enabled | `1` |
| `completion-match` | `Any`, `All` | `Any` |
| `completion-clock` | `Unscaled`, `Scaled` | `Unscaled` |
| `stop-on-completion` | bool | `false` |

Scalar USS names use the same attribute name prefixed with `--meparticles-`.
Vectors use individual components: `--meparticles-gpu-bounds-center-x`, `-y`, `-z`
and `--meparticles-gpu-bounds-size-x`, `-y`, `-z`. Use unitless numbers, `true` / `false`
for booleans and the enum names above. Standard USS controls layout, opacity and clipping.
Every element gets the `meparticles` class.

`prefab` is a UXML GameObject asset reference, selectable in UI Builder; USS does not load
prefabs. The reference is stored in the VisualTreeAsset, requiring neither Resources nor
UnityEditor at runtime. ParticleSystem modules and VFX Graph remain configured on the prefab.
Read-only diagnostics are not attributes.

USS overrides UXML; removing a rule or class restores the UXML value. Attribute changes are
batched into a scheduled update after panel attachment, avoiding partial initialization.
`auto-play` starts initial playback; changing false to true restarts it, while true to false
stops and clears it. Ordinary style updates do not restart a stopped effect. Replacing the
prefab or render mode recreates the backend and resumes a playing effect when auto-play is true.
Completion settings are captured at `Play()`, so edits affect the next run. Invalid values
are reported in the Console.

Use `uxmlPrefab`, `uxmlRenderMode`, `uxmlPixelsPerUnit` and the other `uxml*` properties to
change declarative defaults from C# while retaining USS overrides. The existing `SetPrefab` /
`Play` API and C# settings remain available for programmatically created elements. Avoid mixing
configuration approaches on one element: the next USS update restores its declarative configuration.

Runnable assets: [ParticleMarkup.uxml](Demo/ParticleMarkup.uxml), [ParticleMarkup.uss](Demo/ParticleMarkup.uss).
Run **Tools > Checks > ME.UITK.Particles > Run Markup Checks** to verify import, automatic playback,
attributes, class changes and disposal. Report: `Logs/ParticleUI-Markup.txt`.

### USS animations

Native UI Toolkit `transition-property`, `transition-duration`, `transition-delay` and
`transition-timing-function` animate supported visual properties such as `opacity`, `scale`,
`translate` and `rotate` on every backend. Particle-specific values have additional transitions:

```css
.card-particles {
    opacity: 1;
    scale: 1 1;
    transition-property: opacity, scale, rotate;
    transition-duration: 450ms;
    transition-timing-function: ease-in-out;
    --meparticles-transition-duration: 450ms;
    --meparticles-transition-delay: 0s;
    --meparticles-transition-timing-function: ease-in-out;
}

.animated .card-particles {
    opacity: 0.65;
    scale: 1.1 1.1;
    rotate: 12deg;
    --meparticles-pixels-per-unit: 72;
}
```

`--meparticles-transition-duration` and `--meparticles-transition-delay` accept seconds
(`0.45s`), milliseconds (`450ms`), or unitless seconds. Both default to zero and must be finite
and nonnegative. `--meparticles-transition-timing-function` accepts `linear` (default),
`ease-in`, `ease-out` and `ease-in-out`: linear, quadratic in/out and smoothstep interpolation,
respectively, not CSS cubic-bezier curves. One timing configuration applies to `pixels-per-unit`
and every VFXGraph bounds component. Boolean, enum and completion settings change immediately.

Initial configuration and prefab/render-mode replacement snap to the target. Later changes
interpolate from the current value, including interrupted/reversed transitions. Zero duration
disables interpolation. Time is unscaled; transitions work while playback is stopped without
replacing the backend or calling Play. Detachment snaps to the target and pauses the animation
scheduler; Dispose also cancels it. Native `transition-property` does not drive `--meparticles-*`;
use the dedicated properties above. CSS `@keyframes` and `animation-name` are not implemented.

[ParticleDemo.uxml](Demo/ParticleDemo.uxml) and [ParticleStressTest.uxml](Demo/ParticleStressTest.uxml)
provide the demo layouts. [ParticleClick.uxml](Demo/ParticleClick.uxml) and
[ParticleStressCell.uxml](Demo/ParticleStressCell.uxml) are dynamic effect templates. Saved scenes
and scene builders reference these assets. **Animate USS** in the playground toggles the
animated class; hovering a card demonstrates native scale transitions. Stress measurements
leave animations disabled.

Call `ApplyUxmlSettings()` after editing `uxml*` properties if you need to Play immediately in
the same frame. Otherwise prefer the normal deferred application for UXML and USS class changes.
**Run Markup Checks** also verifies delay, bounds interpolation, reversal and disposal.
**Run Demo Markup Checks** starts from Edit Mode, validates both scenes in Play Mode and returns
to Edit Mode. Report: `Logs/ParticleUI-DemoMarkup.txt`.


## What it does

Add a `ParticleElement` to your visual tree, assign a prefab, and call `Play()`.
Unity runs the original particle simulation; the element presents its output inside UI Toolkit.

- **Reuse your effects.** ParticleSystem modules stay in Unity's native simulation, including child systems and sub-emitters.
- **Choose from five rendering paths.** Auto selects GPU, Mesh, RT or a shared atlas for compatible ParticleSystem effects. Select VFXGraph explicitly for VFX Graph prefabs.
- **Compose with your UI.** Draw behind a button, over its text, or inside a clipped container. Particle elements let pointer input through.
- **Keep the whole effect in frame.** The RT path samples system bounds before playback and expands them at runtime, preserving particle scale.
- **Manage lifetime with the visual tree.** Detaching releases resources; reattaching recreates the effect. `Dispose()` clears its prefab and playback intent.

> This is a prototype adapter, not a conversion into a separate particle engine. Native simulation does not imply identical rendering for every shader or module combination. See [limitations](#limitations).

## Quick start

### Install and run the demo

1. Copy or clone this repository into `Assets/ME.UITK.Particles` in a **Unity 6000.6.0f1** project configured for **URP 17.6.0**. Install **Burst 2.0.0**, **Input System 1.20.0**, and **Unity UI (uGUI) 2.6.0** through Package Manager. Enable the Input System in Player Settings (Active Input Handling: Input System Package or Both).
2. Open [`ParticlePlayground.unity`](Demo/ParticlePlayground.unity).
3. Enter Play Mode and select a preset. Try **Clipping**, **Behind UI**, and **Above UI**.

This repository contains the asset folder, including runtime code, editor tools, and demo assets. Install it under `Assets/ME.UITK.Particles`; the demo builders use this path. It is not a UPM package. Other Unity versions and render pipelines have not been validated.

### Use it in your own project

The code is split into `ME.UITK.Particles` (runtime), `ME.UITK.Particles.Demo`, and `ME.UITK.Particles.Editor` (Editor only). If your scripts use an asmdef, add a reference to `ME.UITK.Particles`. The runtime assembly references Burst and Collections; it does not depend on the demo or editor assemblies.

Copy [`Runtime`](Runtime), including its shaders, `Resources` directory, and `.meta` files, into a compatible Unity project. Burst is required to compile the job.
Use a configured `UIDocument` and reserve **layer 31** for the adapter. Exclude that layer from your scene cameras; the adapter's own cameras render it.

```csharp
using ME.UITK.Particles;
using UnityEngine.UIElements;

// document: your UIDocument; effectPrefab: a GameObject with ParticleSystem(s).
var particles = new ParticleElement { pixelsPerUnit = 60f, };
particles.style.width = 300;
particles.style.height = 220;

// Configure before attaching so the element creates the intended effect.
particles.SetPrefab(effectPrefab, ParticleRenderMode.Auto);
document.rootVisualElement.Add(particles);
particles.Play();
```

The prefab origin maps to the center of the element's `contentRect`. `pixelsPerUnit` controls the scale in UI units; increasing it makes the effect larger.
`Auto` uses the mesh path only for compatible effects with the included `ParticleUI/Alpha` material shader.

<details>
<summary><strong>Complete MonoBehaviour example</strong></summary>

Attach this component alongside a configured `UIDocument`, assign a particle prefab, and enter Play Mode.

```csharp
using ME.UITK.Particles;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public sealed class ParticlePanel : MonoBehaviour {

    [SerializeField] private GameObject effectPrefab;
    private ParticleElement particles;
    private VisualElement container;

    private void OnEnable() {

        this.container = new VisualElement();
        this.particles = new ParticleElement { pixelsPerUnit = 60f, };
        this.particles.style.width = 300;
        this.particles.style.height = 220;
        this.particles.SetPrefab(this.effectPrefab);
        this.container.Add(this.particles);
        this.container.Add(new Button(this.particles.Play) { text = "Celebrate", });
        this.GetComponent<UIDocument>().rootVisualElement.Add(this.container);

    }

    private void OnDisable() {

        this.particles?.Dispose();
        this.container?.RemoveFromHierarchy();
        this.particles = null;
        this.container = null;

    }

}
```

</details>

## Playback completion

Subscribe before calling `Play()`. With default settings, `ParticleCompletedEvent` is dispatched when all native systems have finished, including children and sub-emitters. Looping effects need a threshold to complete while still emitting.

```csharp
effect.completionSettings = new ParticleCompletionSettings {
    timeEnabled = true,
    timeGreaterThan = 2f,
    countEnabled = true,
    countLessThan = 5,
    match = ParticleCompletionMatch.All,
    clock = ParticleCompletionClock.Unscaled,
    stopOnCompletion = true,
};
effect.RegisterCallback<ParticleCompletedEvent>(OnParticlesCompleted);
effect.Play();

void OnParticlesCompleted(ParticleCompletedEvent evt) {
    Debug.Log("Particle playback completed");
}

// When the subscriber is no longer needed:
// effect.UnregisterCallback<ParticleCompletedEvent>(OnParticlesCompleted);
```

- `All` requires every enabled condition; `Any` requires at least one. Enable only one condition for time-only or count-only completion.
- Comparisons are strict: elapsed time **> X**, total live particles **< Y**. Time must be finite and nonnegative; Y must be at least 1. Use Y = 1 to detect zero live particles.
- The initial zero before emission/start delay is ignored until particles have been observed, or all native systems have naturally finished. Later gaps between bursts can satisfy a count threshold; use default natural completion to wait for all future emission.
- The clock starts when an attached instance actually starts playing. Unscaled time is the default; `Scaled` follows Unity game time. It is elapsed wall/game time, independent of individual systems' simulation speed.
- Settings are captured at each `Play()`. The callback runs on the main thread, once per playback, on the element's scheduled update. `hasCompletedPlayback` exposes the result. Handlers can safely restart or dispose the element.
- `stopOnCompletion` defaults to false: notification alone does not interrupt rendering or emission. Set it to true to stop and clear before invoking the callback.
- Manual `Stop()`, `SetPrefab()`, detach and `Dispose()` cancel the pending notification. Reattaching a playing element starts a fresh playback, matching its existing restart behavior. Subscriptions persist until removed with `UnregisterCallback<ParticleCompletedEvent>()`. The pooled event supports trickle-down and bubbling: `evt.target` is the particle element, while `evt.currentTarget` is the element handling the event. Event instances must not be retained after the callback. First-use event pool growth may allocate.

Run **Tools > Checks > ME.UITK.Particles > Run Completion Checks** in a demo's Play Mode to verify threshold boundaries, Any/All, child systems, restart, cancellation and scheduler delivery.

## Draw behind or above UI

![Particles drawn behind a button, then above its background and text](docs/media/layering.gif)

*The capture switches from Behind UI to Above UI. Both rendering paths follow visual-tree order.*

Use an absolute particle element to cover the same area as a button. The two must share a parent for `PlaceBehind` / `PlaceInFront`.

```csharp
var root = document.rootVisualElement;
var container = new VisualElement();
container.style.width = 300;
container.style.height = 160;
container.style.overflow = Overflow.Visible;
root.Add(container);

var button = new Button { text = "Claim reward", };
button.style.alignSelf = Align.Center;
button.style.marginTop = 60;
container.Add(button);

var particles = new ParticleElement { pixelsPerUnit = 60f, };
particles.style.position = Position.Absolute;
particles.style.left = particles.style.right = 0;
particles.style.top = particles.style.bottom = 0;
particles.SetPrefab(effectPrefab);
container.Add(particles);

particles.PlaceBehind(button); // The opaque button covers the particles.
// particles.PlaceInFront(button); // Particles cover the button and its text.
button.clicked += particles.Play;
```

`ParticleElement` and its texture child use `PickingMode.Ignore`, so the button can still receive clicks.
For a mask, set `container.style.overflow = Overflow.Hidden`. Any clipping ancestor can clip the effect, even when the particle element itself allows overflow.

## Rendering paths

`ParticleRenderMode` is a `[Flags]` mask. `Auto` includes GPU, Mesh, RenderTexture and RenderTextureAtlas. Pass any nonempty subset; excluded modes are never used, including fallbacks. VFXGraph remains an explicit standalone mode and cannot be combined with ParticleSystem modes.

```csharp
// All four ParticleSystem paths:
particles.SetPrefab(prefab, ParticleRenderMode.Auto);
// Everything except Atlas:
particles.SetPrefab(prefab, ParticleRenderMode.Auto & ~ParticleRenderMode.RenderTextureAtlas);
// Direct rendering only:
particles.SetPrefab(prefab, ParticleRenderMode.GPU | ParticleRenderMode.Mesh);
// Permit an atlas and its individual RT fallback:
particles.SetPrefab(prefab, ParticleRenderMode.RenderTexture | ParticleRenderMode.RenderTextureAtlas);
```

UXML accepts comma-separated names, for example `render-mode="GPU, Mesh"`. A single flag forces that mode. Empty/unknown masks and VFXGraph combinations are rejected. If no permitted path supports the prefab, `SetPrefab` throws before replacing the current effect. Without RT, Mesh does not calibrate against RT, and an Atlas-only request uses an atlas even for one effect. If its tile exceeds device/size limits, drawing pauses (`activeMode == None`) until the tile fits again; no excluded RT fallback is allocated.

Mode names were renamed: `Shader` -> `GPU` (value `4`), previous `GPU` -> `VFXGraph` (value `16`). Numeric scene/prefab values remain unchanged. Update external C# and UXML names accordingly; an old `GPU` name now selects the ParticleSystem path. `gpuBounds` retains its existing name.

Numeric enum values changed: old Auto `0` is now `15`, VFXGraph `3` is now `16`, Atlas `5` is now `8`; Mesh `1`, RT `2` and GPU `4` are unchanged. Project UXML has been updated to the new mode names. For external serialized numeric data, migrate these old values once before interpreting them as masks; old `3` and `5` now mean valid combinations and cannot be detected automatically.

`RenderTextureAtlas` is an additional native rendering path: up to four compatible effects share one camera, two textures and one alpha-resolve pass. `Auto` keeps GPU priority, compares Mesh/RT for Mesh-compatible effects, and groups eligible RenderTexture candidates (including measured RT winners) when four playing effects have matching tile dimensions/resolution. Existing groups remain shared down to two effects; a lone active effect returns to RT without restarting simulation.

The playground has a separate **SHARED TEXTURE ATLAS** card alongside RT. The stress test offers **RenderTextureAtlas** and quick presets for 4 or 16 elements with 10,000 total particles. Check `activeMode` for the actual path. **Run Atlas Checks** validates final UI pixels, tile isolation/orientation, automatic transitions, stopping, detach/reattach and oversized-tile fallback.

Atlas supports one local, view-aligned ParticleSystem per prefab, with `ParticleUI/Alpha` and particle sorting disabled. World-space trails, gravity, external interactions/shape renderers, additional components and uncertified shaders use RT. Bounds are estimated and checked before rendering; growing effects are regrouped. Tiles above 1024 pixels per axis or 128 world units fall back to RT only when its flag is allowed; otherwise drawing pauses. Atlas pages require 2048-pixel texture support. Stopped elements retain their tiles but do not draw; empty pages are released on disposal.

Atlas reduces camera/resolve overhead, not particle simulation or particle draw calls. Padding and partially occupied pages can increase texture memory/fill cost. Profile groups of effects on the target device; one effect is not expected to benefit.

```csharp
particles.SetPrefab(effectPrefab, ParticleRenderMode.RenderTextureAtlas);
particles.Play();
```

Auto checks GPU first, compares **Mesh / RenderTexture** for Mesh-compatible effects, and uses **RenderTexture / RenderTextureAtlas** for incompatible effects. Measured RT winners can also join Atlas; Mesh remains preferred when frame times are close. VFX Graph requires explicit `ParticleRenderMode.VFXGraph`. Run **Run Render Mode Checks** and **Run Atlas Checks** to verify selection and grouping.

There are **five rendering paths: Mesh, RenderTexture (RT), RenderTextureAtlas, GPU, and VFXGraph / VFX Graph**. `Auto` is a selection policy; VFX Graph remains explicit.

| Mode | Behavior |
| :--- | :--- |
| `Auto` | Selects GPU when compatible; otherwise compares Mesh/RT and groups eligible RT candidates into Atlas. Never selects VFX Graph automatically. |
| `Mesh` | Draws ParticleSystem geometry through UI Toolkit. Explicit Mesh rejects incompatible prefabs without replacing the current effect; it never switches rendering paths. |
| `RenderTexture` (RT) | Renders the native ParticleSystem through a camera into a transparent texture. |
| `RenderTextureAtlas` | Shares a camera and textures across compatible native effects, with RT fallback only when its flag is included. |
| `GPU` | GPU-based rendering for compatible ParticleSystem effects with supported modules. |
| `VFXGraph` / VFX Graph | Explicit mode for a prefab containing `VisualEffect` with an assigned VFX Graph. |

**Mesh** uses `ParticleSystemRenderer.BakeMesh` and UI Toolkit mesh drawing. It currently requires local-space billboard particles, the `ParticleUI/Alpha` shader, and unchanged material UV scale/offset. Trails are baked separately with `BakeTrailsMesh` and their own `ParticleUI/Alpha` material (also without UV scale/offset). Grid and Sprites Texture Sheet Animation use the baked UVs. Sprites must use Full Rect geometry without tight packing and share one texture; the sprite texture takes precedence over the material texture. Particle lights and custom vertex data remain incompatible with Mesh.

**RenderTexture** renders the native system with its materials, resolves alpha for UI composition, and displays the result in an expanded child element. The explicit RenderTexture card uses this path even for GPU-compatible trails.

![Trails compared across rendering modes in the screen overlay](docs/media/trails.gif)


### Procedural GPU from a ParticleSystem prefab

`SetPrefab(prefab)` checks GPU compatibility first. Effect parameters come from the root ParticleSystem and its registered sub-emitters. The backend reads its settings and builds a static seed mesh and lifetime lookup texture; it never instantiates or simulates a ParticleSystem. The vertex shader computes position, rotation, size and color from elapsed time. A camera and transparent RenderTexture still provide UI composition, clipping and world-space presentation.

Supported inputs: one active root system, local simulation, unit scale, one or more guaranteed bursts at explicit times within Duration, no looping/prewarm/start delay, a total of up to 16,000 particles within maxParticles. Start lifetime/speed/size/rotation accept Constant or Two Constants; 3D Start Size supports separate X/Y billboard dimensions and preserves Z for particle access; start color accepts Color or Two Colors. Shape can be disabled, a full random Circle without additional transforms, or a Box volume with position, rotation and scale. Box Shell/Edge and direction randomization remain unsupported. Constant 3D gravity, a single Size over Lifetime curve and a single Color over Lifetime gradient are supported. simulationSpeed, useUnscaledTime, the ParticleUI/Alpha texture and material tint are used. Billboards must be view-aligned, unsorted, with default pivot and flip.

Burst schedules require constant whole counts, probability 1 and one cycle per entry. Simulation splits at birth times, preserving particle ages during large manual steps. Pause freezes the schedule, StopEmitting cancels future waves, Play restarts it, and checkpoints preserve its cursor. Continuous emission, repeating cycles and random burst counts remain unsupported. Box Rain and Emission Waves work unchanged in GPU.

Motion modules in GPU mode:

| Module | Supported settings |
| --- | --- |
| Rotation over Lifetime | Z rotation, Separate Axes disabled. |
| Velocity over Lifetime | Linear X/Y/Z in Local space, zero Orbital/Radial, Speed Modifier = 1. |
| Force over Lifetime | X/Y/Z in Local space, Randomize disabled. |
| Noise | Approximate GPU position turbulence, shared strength or Separate Axes, frequency, damping, constant Scroll Speed and Position Amount, 1–4 octaves with multiplier/scale, quality-dependent field sampling. Remap, Rotation Amount and Size Amount are unsupported. |

Rotation, velocity, force and noise strength accept Constant, Two Constants, Curve and Two Curves. Rates and their integrals use 256 samples across normalized lifetime. Noise without Trails or parent Sub Emitters uses a shared spatial curl field in a compute shader. Frequency controls spatial detail, Scroll Speed moves the field, and the system seed selects its pattern. Neighbouring particles follow coherent flow; their random values select strength curves rather than independent phases. Noise advects position without accumulating into base velocity. This approximates Unity Noise but does not reproduce its exact trajectories. Systems with Trails or parent Sub Emitters retain analytic wave Noise. No native ParticleSystem simulation is used; GetParticles reads GPU state for spatial Noise. Pause, replay, manual emission and SetParticles rebasing retain module motion.

**Trails:** Per Particle trails support local space, Stretch UVs, ratio, constant positive lifetime, Die With Particles, Size Affects Width, Inherit Particle Color, a width curve and color gradients. Assign a `ParticleUI/Alpha` trail material. Ribbon, World Space, lighting data, Size Affects Lifetime and random/multiple trail curves are rejected. GPU strips sample the analytic trajectory with 2–32 segments based on Min Vertex Distance and estimated travel, approximating native point placement. SetParticles reconstructs a live trail from the edited trajectory, so edits can move its history. Expired particles' tails survive Emit/rebasing until their lifetime ends, up to 16,000 retained strips per system. Stop and replay clear history. Natural completion waits for tails even when particleCount is zero.

**Sub Emitters:** Birth and Death support emission probability and Inherit Color/Size/Rotation. Up to eight active direct child systems may use zero local position/rotation, unit scale and the root simulation speed/clock. Each child must otherwise meet GPU compatibility, including one burst; its trails and motion modules are supported. Children emit only when triggered, at the parent's event position and exact simulation time, including large time steps. Collision, Trigger, Manual events, nested sub-emitters and Lifetime/Duration inheritance are rejected. CPU event scheduling creates seeds; particle movement and trails stay shader-driven without native ParticleSystem simulation. Particle APIs and Emit accept a child systemIndex in hierarchy order. particleCount sums the hierarchy; completion waits for children. SetParticles reschedules future natural deaths but does not generate Birth or death events for explicitly removed particles.

The existing Trails and Sub Emitters presets work in the GPU card and Auto. **Run GPU Hierarchy Checks** in Play Mode checks events, inheritance, child APIs, pause, Emit, surviving trail pixels and completion. Report: `Logs/ParticleUI-GPUHierarchy.txt`.


Additional GPU modules (GPU approximations, not native Shuriken simulation):

| Module | Supported settings |
| --- | --- |
| Color / Size by Speed | Speed ranges, gradients/curves and random min/max values; Size supports separate X/Y axes. |
| Rotation by Speed | Z curves and speed range. Analytic rendering integrates eight samples per interval; stateful dynamics integrate per GPU step. |
| Texture Sheet Animation | Grid up to 256 tiles per axis, UV0, Whole Sheet or Single Row, Custom/Random row, Lifetime/Speed/FPS time, cycles and start frame. Sprites use an ordered UV lookup for Full Rect frames sharing one texture, including atlas rotation/flips; tight packing and missing/mixed textures are rejected. MeshIndex is rejected for Single Row. |
| Inherit Velocity | Initial or Current, with a snapshot of Main Custom emitter velocity. Transform/Rigidbody tracking is rejected, including on sub-emitters. |
| Lifetime by Emitter Speed | Curve and speed range applied at birth using Main Custom emitter velocity. |
| Limit Velocity over Lifetime | Local scalar or separate XYZ constant limits, damping, constant drag, size/velocity drag multipliers. |
| Collision | Up to eight snapshot Planes, constant bounce/dampen/lifetime loss, radius scale and kill speeds. No World collisions, messages or collider force. |
| Triggers | Up to eight snapshot SphereColliders, union of their volumes, Kill/Ignore for Inside/Outside/Enter/Exit. No Callback. |
| External Forces | Explicit List of up to eight snapshot spherical directional wind fields, constant XYZ and module multiplier. No field gravity/drag/vortex/vector texture. |
| Lights | GPU billboard glow from a Point Light template without shadows, range/intensity curves, color, ratio, maximum count and particle size/color/alpha options. No scene illumination or Light instances. |
| Custom Data | `GetCustomParticleData(List<Vector4>, ParticleSystemCustomData, systemIndex)` evaluates Vector/Color streams for live particles. Does not supply arbitrary material vertex streams or accept custom-data writes. |

Limit Velocity, Collision, Triggers and External Forces use a compute buffer and GPU state texture. They require compute shaders and writable RGBAFloat textures. The bounded integrator uses steps of about 1/60 second, up to 256 steps per call; large steps and fast particles can miss thin trigger volumes. Their trajectory is an approximation, not Unity Physics. Geometry and force settings are captured by SetPrefab, with no scene tracking. These stateful modules cannot yet share a system with Trails or parent Sub Emitters; the compatibility reason names this combination. Analytic parents can still emit children with GPU dynamics.

Stateful GPU particle counts use asynchronous readback and can lag a frame or more. `GetParticles`, `SetParticles`, Emit and Custom Data access synchronize GPU state when needed and may stall; ordinary simulation stays on GPU. The analytic path continues to count from lifetimes without readback. Glow selects at most the first maxLights particle slots before ratio filtering, so it may render fewer glows than Unity's light allocation.

Run **Run Extended GPU Checks** in Play Mode for appearance pixels, atlas frames, emitter velocity, Custom Data, GPU speed limits/drag, plane bounce, trigger kills, wind, read/edit continuity and glow without native instances. Report: `Logs/ParticleUI-GPUExtended.txt`.

Unsupported module settings, hierarchies, scripts, extra renderers, other materials or unsupported hardware cause Auto to fall back to Mesh/RenderTexture. Explicit `ParticleRenderMode.GPU` rejects incompatible prefabs before replacing the current effect. `ParticleElement.GpuCompatibility(prefab)` returns null when compatible, otherwise a reason; Editor/DEBUG compatibilityInfo also reports the fallback reason.

Curves and gradients use a 256-sample approximation. Random values preserve the configured ranges, but do not reproduce Unity's RNG sequence. Seeds are generated when the backend is created; Play reuses them. Call SetPrefab again to apply changed prefab settings. This is a limited GPU implementation, not complete Shuriken parity. bakedVertexCount is zero; particleCount uses lifetimes for analytic motion and delayed GPU readback for stateful dynamics. Natural completion and Stop use the standard element API.

Run **Tools → Checks → ME.UITK.Particles → Run GPU Checks** in Play Mode to check Auto selection, no ParticleSystem instantiation, visible pixels, completion, replay, Stop, detach/attach and fallback. Report: `Logs/ParticleUI-GPU.txt`. **Run GPU Module Checks** additionally validates motion integrals, ranges, rebasing, Noise and diagnostics; report: `Logs/ParticleUI-GPUModules.txt`.

### Bounds without viewport cropping

Before playback, the adapter samples the hierarchy's native renderer bounds, including children and trails, with **10% padding** and a minimum margin. It then checks live bounds before camera rendering and grows the capture area when needed.

The texture is positioned relative to the emitter, so asymmetric bounds do not shift the effect. Its resolution is capped at **1024 pixels per side**: larger bounds reduce detail, rather than cropping the effect or changing its UI size.

The initial simulation is limited to **15 seconds / 450 steps**. Long-running or randomized effects rely on runtime growth beyond that preview. This is a sampled estimate, not a mathematical bound for arbitrary shader displacement.

## API reference

Particle and vertex counts and thresholds (`particleCount`, `bakedVertexCount`, `jobVertexThreshold`, `countLessThan`) use `uint`. Stress-test `Configure` also takes `uint` counts. When passing signed variables, validate their range before conversion. Unity buffer sizes and collection indices retain `int`.

Namespace: **`ME.UITK.Particles`**

| Member | Purpose |
| :--- | :--- |
| `ParticleElement()` | Create an element; attach it to a panel before using simulation APIs. |
| `SetPrefab(GameObject value, ParticleRenderMode mode = ParticleRenderMode.Auto)` | Replace the prefab, release the backend and reset playback intent. Explicit Mesh/GPU validate compatibility; VFXGraph requires a VFX prefab. |
| `Play() / Stop()` | Restart from the beginning / immediately stop and clear. |
| `Pause() / Resume() / StopEmitting()` | Freeze simulation and completion time / continue / drain existing particles without new emission. |
| `Emit(uint count, int systemIndex = 0)` | Add a burst using configured start parameters. |
| `EmitIn(VisualElement target, uint count, int systemIndex = 0)` | Add a burst across another element’s current rectangular bounds. |
| `SetPosition(Vector2 offset) / positionOffset` | Set/read the UI offset from contentRect.center; default zero. |
| `SetTint(Color color) / tint` | Set/read the final color multiplier; default white, finite channels in [0, 1]. |
| `SetVisualScale(float value) / visualScale` | Set/read the visual multiplier of pixelsPerUnit; default 1, finite and positive. |
| `SetSystemScale(Vector3 value) / systemScale` | Set/read the multiplier of the prefab root scale; default Vector3.one, finite positive axes. |
| `simulationSpeed` | Read/write speed multiplier, default 1, finite and nonnegative. Zero does not pause completion time. |
| `pixelsPerUnit` | Read/write UI units per simulation unit, default 65f; use a finite positive value. |
| `GetParticles(ParticleSystem.Particle[] particles, int size = -1, int offset = 0, int systemIndex = 0)` | Return the number copied to a reusable buffer. |
| `SetParticles(ParticleSystem.Particle[] particles, int size = -1, int offset = 0, int systemIndex = 0)` | Write particles from a buffer without restarting playback. |
| `GetParticleCount(int systemIndex = 0)` | Return uint live count for one system. |
| `SimulationToLocal(Vector3 position, int systemIndex = 0) / LocalToSimulation(Vector3 position, int systemIndex = 0)` | Convert between simulation coordinates and element-local UI coordinates; return Vector3. |
| `supportsParticleAccess / particleSystemCount` | Read-only access support and system count: native hierarchy / GPU 1 / VFXGraph 0. |
| `capabilities` | Read-only ParticleCapabilities flags; None until initialized. |
| `isAlive / isPlaying / isPaused` | Read-only live-or-pending-emission / active unpaused run / explicit pause state. |
| `completionSettings / hasCompletedPlayback` | Read/write completion settings / read-only result of the current run. |
| `RegisterCallback<ParticleCompletedEvent>() / UnregisterCallback<ParticleCompletedEvent>()` | Inherited UITK subscription API for the completion event. |
| `activeMode / isAutoSelecting` | Read-only selected rendering mode / whether Auto is still choosing. |
| `compatibilityInfo` | Read-only diagnostic explanation in Editor or DEBUG; placeholder otherwise. |
| `hasInstance` | Read-only backend initialization state, including GPU and VFXGraph. |
| `particleCount / bakedVertexCount / renderBounds` | Read-only total live count / latest baked vertex count / backend render bounds. VFXGraph counts are asynchronous. |
| `useJobs / jobVertexThreshold` | Read/write Mesh job settings; defaults true / 4096u. |
| `gpuBounds` | Read/write VFXGraph capture Bounds, default center zero and size (10,10,10); finite values and positive size. |
| `ParticleElement.MeshCompatibility(GameObject source)` | Return the first MeshCompatibilityResult. |
| `ParticleElement.GpuCompatibility(GameObject source)` | Return null if compatible, otherwise an explanatory string. |
| `ApplyUxmlSettings()` | Immediately apply pending uxml* settings with current USS overrides. |
| `Dispose()` | Release resources and clear the prefab/playback intent; remove from hierarchy separately. |

```csharp
// Replace the current effect and explicitly choose the texture path.
particles.SetPrefab(trailsPrefab, ParticleRenderMode.RenderTexture);
particles.Play();

Debug.Log($"{particles.activeMode}: {particles.compatibilityInfo}");
Debug.Log($"Live particles: {particles.particleCount}");

particles.Stop();
particles.Dispose();
particles.RemoveFromHierarchy();
```

### Lifecycle, presentation and diagnostics

The snippets below are independent usage examples. `root` is an attached VisualElement, `effectPrefab` is a ParticleSystem prefab, and `vfxPrefab` is a VFX Graph prefab. Import the three namespaces shown below. Read diagnostics after initialization; counts and bounds update during rendering.

```csharp
using ME.UITK.Particles;
using UnityEngine;
using UnityEngine.UIElements;

var particles = new ParticleElement {
    pixelsPerUnit = 65f,
    useJobs = true,
    jobVertexThreshold = 4096u,
    simulationSpeed = 1f,
};
particles.style.width = 400f;
particles.style.height = 240f;
particles.SetPrefab(effectPrefab, ParticleRenderMode.Auto);
root.Add(particles);
particles.Play();

particles.Pause();
Debug.Log($"Paused: {particles.isPaused}, alive: {particles.isAlive}");
particles.Resume();
particles.StopEmitting();
Debug.Log($"Playing: {particles.isPlaying}, completed: {particles.hasCompletedPlayback}");

particles.SetPosition(new Vector2(10f, -5f));
particles.SetTint(new Color(1f, .8f, .5f, .75f));
particles.SetVisualScale(1.5f);
particles.SetSystemScale(new Vector3(2f, 1f, 1f));
Debug.Log($"Offset: {particles.positionOffset}, tint: {particles.tint}");
Debug.Log($"Scales: {particles.visualScale}, {particles.systemScale}");

Debug.Log($"Backend: {particles.hasInstance}, mode: {particles.activeMode}");
Debug.Log($"Selecting: {particles.isAutoSelecting}, {particles.compatibilityInfo}");
Debug.Log($"Particles: {particles.particleCount}, vertices: {particles.bakedVertexCount}");
Debug.Log($"Bounds: {particles.renderBounds}, systems: {particles.particleSystemCount}");

particles.Stop();
particles.Dispose();
particles.RemoveFromHierarchy();
```

Check capabilities on an initialized, attached element before optional operations. Run the following after layout, for example inside a button click handler, not after Dispose:

```csharp
if ((particles.capabilities & ParticleCapabilities.EmitIn) != ParticleCapabilities.None) {
    particles.EmitIn(button, 32u);
}
if ((particles.capabilities & ParticleCapabilities.Emit) != ParticleCapabilities.None) {
    particles.Emit(8u, systemIndex: 0);
}
```

`ParticleCapabilities` is a flags enum: `None`, `Pause`, `StopEmitting`, `SimulationSpeed`, `Position`, `Tint`, `Scale`, `ParticleAccess`, `Emit`, `EmitIn`, `CoordinateConversion`. All backends support the first six features; only native and GPU support the last four. `ParticleRenderMode` values are `Auto`, `Mesh`, `RenderTexture`, `RenderTextureAtlas`, `GPU`, `VFXGraph`. Auto checks GPU first, then selects a native rendering path when GPU is incompatible. VFXGraph requires explicit selection.

### Reusable buffer and coordinate conversion

Allocate the buffer once and call `MoveParticles` from your update after attachment, Play and resolved layout. This example moves particles 20 UI units per second, preserving depth and accounting for the selected simulation space and effect scales. Grow the buffer only when needed. Use a valid child index to edit another native system.

```csharp
var buffer = System.Array.Empty<ParticleSystem.Particle>();

void MoveParticles(int systemIndex) {

    if (particles.supportsParticleAccess == false) {
        return;
    }

    var required = checked((int)particles.GetParticleCount(systemIndex));
    if (buffer.Length < required) {
        System.Array.Resize(ref buffer, required);
    }

    var count = particles.GetParticles(buffer, systemIndex: systemIndex);
    for (var i = 0; i < count; i++) {
        var local = particles.SimulationToLocal(buffer[i].position, systemIndex);
        local.x += 20f * Time.deltaTime;
        buffer[i].position = particles.LocalToSimulation(local, systemIndex);
    }

    particles.SetParticles(buffer, size: count, offset: 0, systemIndex: systemIndex);

}

MoveParticles(systemIndex: 0);
```

For a partial read, use `GetParticles(buffer, size: 8, offset: 4, systemIndex: 0)` with a buffer of at least eight entries, and pass the returned count to `SetParticles(buffer, size: count, offset: 4, systemIndex: 0)`. Offset is the starting particle in the system, not an array index. GPU requires offset <= live count and replaces the tail after that offset; do not use partial writes to preserve an untouched tail. See [per-particle control](#per-particle-control) for supported fields and lifetime handling.

### GPU compatibility and VFXGraph bounds

```csharp
var shaderReason = ParticleElement.GpuCompatibility(effectPrefab);
if (shaderReason == null) {
    particles.SetPrefab(effectPrefab, ParticleRenderMode.GPU);
    particles.Play();
} else {
    Debug.Log(shaderReason);
}

particles.gpuBounds = new Bounds(Vector3.zero, new Vector3(20f, 12f, 10f));
particles.SetPrefab(vfxPrefab, ParticleRenderMode.VFXGraph);
particles.Play();
Debug.Log(particles.gpuBounds);
```

VFXGraph bounds configure capture, not the VFX Graph’s own culling bounds. VFXGraph particle access (`GetParticles`, `SetParticles`, `GetParticleCount`), manual emission and coordinate conversion throw `NotSupportedException`. Missing initialization throws `InvalidOperationException` for particle access. Invalid ranges, scales, tint or unresolved emission geometry throw argument/state exceptions; wait for layout rather than passing zero-size elements.

### All declarative C# properties

The `uxml*` properties correspond to the attributes in [UXML and USS](#uxml-and-uss). ApplyUxmlSettings flushes their scheduled application before an immediate Play. USS overrides still take priority. These aliases are a separate configuration approach; position, tint, scales and speed currently use the imperative API above.

```csharp
var particles = new ParticleElement {
    uxmlPrefab = effectPrefab,
    uxmlRenderMode = ParticleRenderMode.Auto,
    uxmlAutoPlay = false,
    uxmlPixelsPerUnit = 65f,
    uxmlUseJobs = true,
    uxmlJobVertexThreshold = 4096u,
    uxmlGpuBoundsCenter = Vector3.zero,
    uxmlGpuBoundsSize = new Vector3(10f, 10f, 10f),
    uxmlCompletionTimeEnabled = true,
    uxmlCompletionTimeGreaterThan = 2f,
    uxmlCompletionCountEnabled = true,
    uxmlCompletionCountLessThan = 1u,
    uxmlCompletionMatch = ParticleCompletionMatch.All,
    uxmlCompletionClock = ParticleCompletionClock.Unscaled,
    uxmlStopOnCompletion = true,
};
particles.style.width = 400f;
particles.style.height = 240f;
root.Add(particles);
particles.ApplyUxmlSettings();
particles.Play();
```

For all `ParticleCompletionSettings` fields, `ParticleCompletionMatch.Any` / `All`, `ParticleCompletionClock.Unscaled` / `Scaled`, and event subscription/unsubscription, see the complete [playback completion example](#playback-completion).

**Run Texture Sheet Checks** validates all four frame colors in Grid/Sprites mode through final UI pixels in Mesh (with and without Jobs), GPU and RenderTexture. Report: `Logs/ParticleUI-TextureSheet.txt`. The existing `TextureSheetAnimationEnabled` compatibility value now identifies an invalid sprite list, not every enabled sheet.

### Check compatibility before creating an element

```csharp
var result = ParticleElement.MeshCompatibility(effectPrefab);
if (result == MeshCompatibilityResult.Compatible) {
    Debug.Log("The prefab can use the mesh path.");
} else {
    Debug.Log($"RenderTexture fallback: {result}");
}
```

`MeshCompatibilityResult` values: `Compatible`, `MissingPrefab`, `NoParticleSystem`,
`UnsupportedSimulationSpace`, `TrailsEnabled`, `LightsEnabled`, `TextureSheetAnimationEnabled`,
`CustomDataEnabled`, `MissingRenderer`, `UnsupportedRenderMode`, `MissingMaterial`,
`MissingShader`, `UnsupportedShader`, and `MaterialUvTransform`.
The method returns one result, not a flags combination. `compatibilityInfo` remains a readable UI description.

Instances are created while attached to a panel: runtime panels require Play Mode; editor panels (EditorWindow and UI Toolkit Inspectors) also work in Edit Mode. Detach releases cameras, textures, meshes, and the prefab instance; attach recreates them and honors the retained playback intent. A detached effect restarts rather than resuming its previous simulation time.

## Playground

Each panel includes a **VFXGraph** card with the VFXGraph Burst sample. Preset buttons change the Auto, RenderTexture and Atlas cards; emit, clear, repeat and layering controls apply to all four cards.

Additional module presets: **Color over Lifetime**, **Size Pulse**, **Rotation over Lifetime**, **Velocity Drift**, **Force over Lifetime**, **Noise Turbulence**, **Limit Velocity**, **Box Rain**, **Emission Waves**, **Texture Sheet Animation** (4-frame grid), and **Texture Sprite Atlas** (4 sprites sharing one texture). Use **Tools → ME.UITK.Particles → Update Demo Presets** to regenerate the preset assets and update their references in the existing playground scene. This overwrites generated preset edits. Unsupported GPU or Mesh combinations display their compatibility reason; VFXGraph continues to use its own VFX Graph.

The demo contains **Sparks**, **Confetti**, **Trails**, and **Sub Emitters** prefabs, presented in screen overlay and a native world-space `UIDocument`. Each panel compares Auto against forced RenderTexture. The world-space panel is tilted 18° around X.

| Control | Action |
| :--- | :--- |
| Preset buttons | Replace the particle prefab. |
| Emit particles / Emit here | Restart all effects / one effect. |
| Repeat | Restart every 2.8 seconds. |
| Clear | Stop repetition and clear particles. |
| Clipping | Apply the viewport mask. |
| Behind UI / Above UI | Change sibling order around an opaque, clickable button. |

Assign your own prefabs through `ParticleDemo.prefabs` in the Inspector.

**Tools → ME.UITK.Particles → Open Demo** opens the existing scene. `Build and Open Demo` regenerates the sample scene and assets in `Demo/Generated`. **It overwrites manual changes to those generated assets.** Opening the existing scene does not require regeneration.

## Stress test

Auto selection runs inside `ParticleElement`. Mesh-compatible effects that cannot use GPU compare Mesh and RenderTexture on the same live simulation (0.5-second warm-up and at least 1 second / 12 frames per path). Sampling starts with Mesh and pauses while playback is paused or the application lacks focus. Selection repeats when the live count changes by more than 2x, or jobs, the job vertex threshold, density or element size change. When RT wins, eligible effects can join Atlas without restarting simulation; Atlas grouping uses the group-size heuristic, not a separate timing comparison. RT must beat Mesh by 5%; frame caps and unrelated frame load affect the result. `isAutoSelecting` reports calibration, `activeMode` reports the current path and `compatibilityInfo` includes frame times. VFXGraph remains explicit.

VFXGraph uses `Demo/Stress/VFXGraph Stress.prefab`, a separate VFX Graph with exposed `ParticleCount`, `SizeMin` and `SizeMax`. Counts are distributed across the selected UI elements; overdraw changes particle size. The graph has a fixed capacity of 50,000 per instance and emits a 30-second burst, so Apply / restart and Measure both restart it. Its static elliptical distribution differs from the native ParticleSystem box with noise: compare renderer workloads with this difference in mind. VFXGraph alive counts arrive with a delay; Mesh jobs does not apply to VFXGraph.


Open [`ParticleStressTest.unity`](Demo/ParticleStressTest.unity) and enter Play Mode.

- Choose **1,000 / 5,000 / 10,000 / 25,000 / 50,000** particles across **1 / 4 / 16** UI elements.
- Compare **Auto**, **Mesh**, **RenderTexture**, **RenderTextureAtlas**, and **VFXGraph**. **Mesh jobs** toggles worker-thread vertex conversion; press **Apply / restart** after changing the workload.
- Enable **Large particles / overdraw** to increase transparent pixel coverage.
- **Measure 10 seconds** restarts the effect, warms up for 2 seconds, then records average FPS, average/worst frame time, and whole-frame GC bytes with the HUD frozen.

GC here includes the entire Unity frame, Editor overhead, and other systems. It is separate from the scoped zero-allocation adapter benchmark. Compare runs at the same resolution, quality settings, focus and profiler settings; use a Player build for representative results. RT creation includes synchronous bounds pre-simulation and can stall with large workloads.

The dedicated [`StressParticles.prefab`](Demo/Stress/StressParticles.prefab) uses a seeded burst and a 30-second lifetime. Rebuild only this scene with **Tools > ME.UITK.Particles > Build and Open Stress Test**. Run **Run Stress Checks** in Play Mode to verify live particle counts, Mesh/RT selection, large-mesh chunking, and clearing across 18 configurations.

### Mesh jobs

For large Mesh effects, vertex position and color conversion runs through Unity's Job System. This also applies when **Auto** selects Mesh. Small effects retain the synchronous path. Native `BakeMesh`, source-data copying and UI submission still run on the main thread; RenderTexture uses the existing camera path.

```csharp
var effect = new ParticleElement {
    useJobs = true,
    jobVertexThreshold = 4096,
};
effect.SetPrefab(prefab, ParticleRenderMode.Auto);
```

UITK owns the temporary output buffers and waits for the registered jobs before consuming them via [`AddMeshGenerationJob`](https://docs.unity.cn/6000.1/Documentation/ScriptReference/UIElements.MeshGenerationContext.html). Each job works on its own mesh chunk; no Unity objects or managed collections are passed to workers. `ParticleVertexJob` is Burst-compiled from its own runtime file, with an explicit Burst package dependency. Synchronous compilation can add a one-time Editor warm-up cost on first use. Scheduling and data copying have a cost, so compare with `useJobs = false` on your target device. The existing Confetti allocation measurements cover the small synchronous path, not the new jobs path.

## Limitations

- Validated with **Unity 6000.6.0f1 / URP 17.6.0**. The tested material is the included alpha-blended `ParticleUI/Alpha` shader. Additive, distortion, soft-particle, and lit shaders may differ from their original scene rendering. A single transparent layer cannot reproduce arbitrary blending with UI behind it.
- RT cameras look along the Z axis at the XY plane. The prefab root rotation is reset; design your effect for that view.
- Each element owns a prefab instance and a camera; RT elements also allocate textures. There is no pooling. Pre-simulation is synchronous and may cause a spike for expensive prefabs. Profile your intended effect count and target devices.
- RT presentation has roughly one frame of latency. Large effects may lose sharpness at the texture resolution cap.
- Instances live far from the scene on layer 31, spaced 100 world units apart. Extremely large effects can overlap neighboring capture areas. Keep that layer exclusive to this adapter.
- Scripts on particle prefabs also run. Prefer visual-only prefabs. Collision, triggers, and external forces do not automatically inherit the original scene's environment.
- The adapter preserves the prefab's time settings, forces `AlwaysSimulate`, and disables particle stop actions so the instance remains under its control. Demo prefabs use unscaled time and fixed seeds.

## Performance & allocations

**Zero managed allocations in the measured, warmed rendering paths** means no `GC.Alloc` events were observed inside the adapter's Mesh drawing and RT preparation/presentation methods in the included Confetti benchmark. It is not a guarantee of zero allocations for an entire Unity frame, arbitrary prefabs, or the component's complete lifetime.

- Mesh drawing reuses buffers, transforms each unique vertex once, caches repeated vertex colors, and uses indices instead of duplicating vertices for small meshes. Large meshes retain safe triangle chunks.
- Mesh buffers are preallocated for up to 1,024 particles per renderer and grow geometrically when needed. They retain peak capacity for reuse.
- RT dimensions grow in 64-pixel blocks and retain capacity until release. A finished camera frame is resolved once; unchanged layout values are not rewritten. Texture growth still allocates native/GPU resources and managed wrappers.
- `Play()` reuses a cached list of non-sub-emitter systems. `Stop()` pauses scheduled updates. RT effects do not allocate unused bake meshes.
- Compatibility checks use a pooled scratch list. Constant bounds-preview curves avoid curve sampling and temporary `params` arrays.
- Demo counters update at most 10 times per second and only create new text when values change.

**Allocations still occur** when creating an element or prefab instance, attaching/recreating resources, replacing a prefab, growing buffers/textures, first-use event pool growth, or updating demo labels. Pooled scratch lists allocate on their first use or growth. The bounds pre-simulation still runs synchronously when an RT instance is created. There is no pool of prefab instances or cameras.

### Reproduce the measurement

In Play Mode, use **Tools → Checks → ME.UITK.Particles → Run Performance Checks**. Results are written to `Logs/ParticleUI-Performance.txt`.

The benchmark uses Unity's `GC.Alloc` recorder on the current thread, first verifying that a known `new byte[4096]` produces one event. It reports **allocation event counts**, not allocated bytes. Mesh measurements wrap actual UI drawing callbacks after warm-up; RT methods and `Play()` are measured with 32 warmed direct calls. RT presentation is supplied with a completed frame on each call so it exercises the resolve/blit path. CPU timings exclude GPU execution, native memory allocation, unrelated Editor work, and the rest of the rendering pipeline.

Use Unity Profiler in a development build on your target device to assess total frame cost, peak memory, first-use spikes, and your own prefab scripts. Retaining buffer capacity trades memory for fewer allocations. See [performance.md](docs/performance.md) for measurement details.

## Validation & capture

In the playground's Play Mode, run **Tools → Checks → ME.UITK.Particles → Run Smoke Checks**.
The checks cover both panels and all four presets: live particles, mesh vertices, RT alpha, transparent texture borders, visible pixels beyond the old viewport, UI scale, Auto selection, clearing, and detach/reattach cleanup.
Results are written to `Logs/ParticleUI-Smoke.txt`. These checks do not replace visual review or world-space input testing.

**Tools → Checks → ME.UITK.Particles → Run Color Checks** compares final screen pixels for Mesh and RT using stationary particles, white/tinted materials, opaque/translucent colors, and both `Apply Active Color Space` settings, including the jobs path. It writes `Logs/ParticleUI-Color.txt`. Keep the Game View visible during the checks.
The mesh path converts baked colors to the sRGB vertex tint expected by UI Toolkit, while matching the material tint multiplication used by the native shader.

To regenerate the GIFs, use **Tools → Development → ME.UITK.Particles → Capture README Frames**, then follow [the capture instructions](docs/media/README.md). All previews are recorded from the actual Unity demo.

---

Author: [Alex Silaev](https://chromealex.com)

[Runtime source](Runtime) | [Demo source](Demo) | [Русский](README.ru.md)

## Modules and GPU rendering

The runtime is split into `Core` and five rendering modules behind `IParticleBackend`:

- `Core`: `ParticleElement`, mode selection, scheduling, completion events, instance lifetime, and shared texture presentation.
- `Modules/Mesh`: compatibility checks, `BakeMesh`, vertex conversion, and Burst jobs.
- `Modules/RenderTexture`: native ParticleSystem rendering and estimated/live bounds.
- `Modules/RenderTextureAtlas`: shared camera/texture pages and automatic grouping.
- `Modules/GPU`: shader-based rendering for compatible ParticleSystem effects.
- `Modules/VFXGraph`: VFX Graph simulation and rendering, with no `BakeMesh` or per-particle CPU geometry conversion.
- `Modules/Shared`: the native ParticleSystem playback implementation shared by Mesh and RenderTexture.

These are code modules in the existing Unity assembly, not separately installable packages. The public namespace remains `ME.UITK.Particles`. `Auto` selects GPU, Mesh, RenderTexture or RenderTextureAtlas; VFX Graph is never selected automatically. VFXGraph is explicit and requires a prefab containing `VisualEffect` components with assigned graph assets, without ParticleSystem components.

Install **Visual Effect Graph 17.6.0** alongside **URP 17.6.0**. VFXGraph mode requires compute shader support and a VFX-compatible render pipeline/device. It renders into a transparent texture and uses the same UI texture presentation as RenderTexture; simulation remains in VFX Graph.

```csharp
var particles = new ParticleElement {
    pixelsPerUnit = 40f,
    gpuBounds = new Bounds(Vector3.zero, new Vector3(10f, 10f, 10f)),
    completionSettings = new ParticleCompletionSettings {
        timeEnabled = true,
        timeGreaterThan = 3f,
        stopOnCompletion = true,
    },
};
container.Add(particles);
particles.SetPrefab(vfxPrefab, ParticleRenderMode.VFXGraph);
particles.Play();
```

`gpuBounds` is the camera volume relative to the effect instance origin, in world units; it defaults to a 10-unit cube. It does not change the graph's own culling bounds. Use local-space graph effects, valid graph bounds, and transparent alpha-blended outputs for predictable UI composition. World positions explicitly authored into a graph must account for the adapter placing instances away from the scene. Additive-only outputs with zero alpha do not compose correctly through the UI texture.

`Play()` resets the graph and sends its configured initial event; `Stop()` disables simulation/rendering and clears the presented texture. `particleCount` uses Unity's delayed GPU readback. Natural completion waits until particles have been observed and all systems report asleep; empty effects or very short bursts missed by readback may never meet that condition. Use a time threshold for deterministic completion timing. `bakedVertexCount` is always zero in VFXGraph mode. This is not a zero-CPU-overhead renderer: cameras, UI composition and event polling still cost CPU/GPU time.

Use **Tools > ME.UITK.Particles > Create VFXGraph Sample** to create `Demo/VFXGraph/VFXGraph Burst.prefab` and its graph from Unity's Simple Burst template. **Run VFXGraph Checks** in Play Mode verifies visible pixels, counts, completion, stopping, detaching and replay in the scene's UI documents. Existing rendering and completion checks continue to cover Mesh and RenderTexture.

## Per-particle control

`supportsParticleAccess` is true for Mesh, RenderTexture, RenderTextureAtlas and
GPU, including Auto when it selects any of these paths. VFXGraph does not support
this API and throws `NotSupportedException`. Before initialization or after disposal,
access throws `InvalidOperationException`.

GPU exposes the root (`systemIndex = 0`) and registered sub-emitter systems in local simulation space. It supports
position, velocity, startLifetime, remainingLifetime, startColor, uniform startSize or nonuniform startSize3D (billboard X/Y),
2D rotation and angularVelocity. Other Particle fields are retained but do not add
unsupported rendering features or simulation modules. GetParticles evaluates the
last rendered simulation time; SetParticles rebases motion and lifetime curves at
that time and updates the mesh. Gravity continues to apply. No native ParticleSystem
is created. Play restores the original prefab burst after edits.
GPU limits writes to `min(maxParticles, 16000)` and rejects offsets beyond the
live count. A write retains the prefix before offset and replaces the rest with the
supplied particles; nonpositive remainingLifetime entries are removed. Mesh/buffer
growth can allocate, and writes upload geometry; unchanged simulation stays on the GPU.

`GetParticles` / `SetParticles` use a reusable `ParticleSystem.Particle[]` and Unity's
native semantics. `size` defaults to -1 (the whole buffer); `offset` defaults to 0
and addresses particles in the system. Pass the returned count back to SetParticles.
Sizes and offsets use `int` to match Unity's API. Invalid buffer ranges throw.

`particleSystemCount` includes inactive child systems; `systemIndex` defaults to 0,
the first system in `GetComponentsInChildren<ParticleSystem>(true)` order. Indices
remain valid for the current instance only. `GetParticleCount(systemIndex)` returns
the count for that system; `particleCount` remains the total across all systems.

Position and velocity use the selected system's simulation space, not UI pixels.
For Local space, coordinates are relative to that system's transform. World-space
positions include the adapter's offscreen instance placement; Custom space follows
the configured custom transform. Calls do not convert coordinates or restart playback.
Use this during playback; SetParticles does not resume an element stopped with Stop.
Negative remainingLifetime removes a particle. Read-modify-write in the same frame,
and reuse the buffer between frames (grow it when necessary).

```csharp
// Allocate once, after attachment and Play(). Local simulation space in this example.
var buffer = new ParticleSystem.Particle[particles.GetParticleCount()];

// Each update:
var count = particles.GetParticles(buffer);
for (var i = 0; i < count; i++) {
    buffer[i].position += Vector3.up * Time.deltaTime;
}

particles.SetParticles(buffer, count);
// Child system: GetParticles(buffer, systemIndex: 1), SetParticles(buffer, count, systemIndex: 1).
```

## Editor UI Toolkit

`ParticleElement` works in `EditorWindow.rootVisualElement` and UI Toolkit custom Inspectors without entering Play Mode. Keep the library's `Editor` folder, and reference `ME.UITK.Particles` from your editor assembly. Put the consuming window script in an `Editor` folder. No UIDocument or PanelSettings is needed for an editor window.

```csharp
using ME.UITK.Particles;
using UnityEditor;
using UnityEngine;

public sealed class MyParticleWindow : EditorWindow {

    private ParticleElement particles;

    [MenuItem("Window/My Particles")]
    public static void Open() {

        GetWindow<MyParticleWindow>();

    }

    public void CreateGUI() {

        this.particles?.Dispose();
        this.rootVisualElement.Clear();
        this.particles = new ParticleElement();
        this.particles.style.flexGrow = 1;
        this.particles.style.minHeight = 240;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/MyEffect.prefab");
        this.particles.SetPrefab(prefab);
        this.rootVisualElement.Add(this.particles);
        this.particles.Play();

    }

    private void OnDisable() {

        this.particles?.Dispose();

    }

}
```

Use **Tools > ME.UITK.Particles > Editor Preview** to select a prefab and rendering mode. **Run Editor Checks** checks Mesh, RenderTexture, Atlas, GPU, Auto and VFXGraph in an actual editor window and writes `Logs/ParticleUI-Editor.txt`.

Edit Mode uses real editor time for both completion clocks, manual ParticleSystem/VFX simulation, and explicit camera render requests. Auto uses compatibility selection (GPU, otherwise Mesh or texture paths), without the Play Mode FPS benchmark. Texture previews require a pipeline supporting `RenderPipeline.StandardRequest`; verified with this project's URP 17.6.0 on Unity 6000.6.0f1. Runtime UIDocuments still require Play Mode.

Detach, Dispose, assembly reload and Play Mode transitions release preview resources. With domain reload disabled, attached editor elements restart on mode transitions. With domain reload enabled, rebuild the UI in CreateGUI. Ordinary prefab MonoBehaviour Update logic is not simulated by this adapter.

## Effect scale

```csharp
particles.SetVisualScale(2f);
particles.SetSystemScale(new Vector3(1.5f, 2f, 1f));
```

These independent settings default to 1 and Vector3.one. Read them through
`visualScale` and `systemScale`. Both can be set before attachment or during playback,
are retained across Play/SetPrefab/reattachment, and do not restart the effect.
All values must be finite and strictly positive; invalid values throw ArgumentOutOfRangeException.

Visual scale multiplies pixelsPerUnit for all rendering modes, including Auto, GPU
and VFXGraph, without changing particle simulation coordinates or the element's layout size.
System scale multiplies the prefab root Transform scale per axis. Native systems keep
their configured scalingMode and simulation space: World-space particles and Shape-only
scaling retain Unity's behavior. GPU scales its local mesh. VFXGraph graphs retain their
own space settings; the capture gpuBounds are scaled by the system multiplier, while
VFX Graph culling bounds remain the graph's responsibility. The two scales multiply visually.

## Pause, resume and drain

`Pause()` freezes the current simulation and the completion clock while keeping the
particles visible. `Resume()` continues that run without clearing particles or sending
a new play event. Calls are idempotent; without an initialized playing instance they
are no-ops. `isPaused` reports the state. A new Play, Stop, SetPrefab, detach or Dispose
resets pause. Detach still releases the simulation; reattachment restarts playback.

`StopEmitting()` preserves current particles and completion tracking. It does not
unpause the effect; call Resume to let paused particles finish. Native backends stop
all systems' emission and restore their emission settings on the next Play. GPU cancels future scheduled bursts while keeping existing particles and child events.
VFXGraph sends the OnStop event: the VFX Graph must handle it through the usual spawn stop
input or a custom OnStop handler. Arbitrary graphs can ignore this event. VFXGraph counts
remain asynchronous; time thresholds remain useful for completion.

## Simulation and UI coordinates

After layout has resolved (GeometryChangedEvent), use:

```csharp
var local = particles.SimulationToLocal(buffer[0].position, systemIndex: 0);
local.x += 20f;
buffer[0].position = particles.LocalToSimulation(local, systemIndex: 0);
```

The methods use Vector3 to retain depth for round trips. X/Y are coordinates local to
ParticleElement, with UI's downward Y axis; Z is effect depth in UI units. A Vector2
implicitly supplies Z=0, selecting the effect's XY plane. Use ChangeCoordinatesTo or
WorldToLocal first when the target comes from another VisualElement or panel coordinates.
Both scales and pixelsPerUnit are included. Native Local/World/Custom simulation spaces
and the selected child system are supported; GPU uses its single local system.
VFXGraph throws NotSupportedException because graph simulation spaces are not exposed by this API.
Missing instances, unresolved layout and non-invertible transforms cannot be converted.

## Runtime controls and button bursts

```csharp
particles.simulationSpeed = .5f; // Multiplier of the prefab's simulation speed.
particles.SetPosition(new Vector2(10f, -5f)); // Offset from the element's center, in UI units.
particles.SetTint(new Color(1f, .8f, .5f, .75f));
particles.Emit(12u); // Additional particles in system 0, without resetting existing particles.

// The ParticleElement should overlay the desired area and share a panel with the button.
// A click occurs after layout, so the button's size and transforms are available.
button.clicked += () => particles.EmitIn(button, 32u);
```

`EmitIn(VisualElement target, uint count, int systemIndex = 0)` emits a one-shot burst
uniformly across the target's rectangular border box. It accounts for target translation,
rotation, scale, nested layouts, the effect's position offset and both effect scales.
The target and effect must share a panel and have resolved, nonzero layout. The method
samples current geometry on each call; it does not subscribe to the target or move
existing particles after resize. Rounded corners, clipping masks and transparent image
regions do not restrict the sampled rectangle. Particles can leave the rectangle as they move.

`Emit(uint count, int systemIndex = 0)` uses the system's configured start parameters.
Native EmitIn overrides only the spawn position and bypasses Shape positioning; other
start parameters remain native. GPU Emit reuses its sampled start templates, and
EmitIn overrides their positions. Both preserve existing particles and keep paused
runs paused. Requested counts are capped to available maxParticles capacity (GPU:
min(maxParticles, 16000)); counts above int.MaxValue throw before reaching Unity.
VFXGraph Emit and EmitIn throw NotSupportedException because arbitrary VFX Graphs do not
provide a standard manual particle-spawn contract.

An Emit/EmitIn call on a stopped, initialized effect starts a manual run, suppressing
its automatic prefab burst/emission for that run. No preliminary Play is needed.
A later Play restores ordinary prefab playback. Emission during a running effect adds
to its current particles; emission after a reported completion arms a new completion
cycle without restarting the simulation. Zero-count calls do not start a run.

`simulationSpeed` defaults to 1 and multiplies the original speed/playRate of each
system. It accepts finite nonnegative values, including zero. Speed zero freezes
simulation but does not pause completion time thresholds; use Pause to freeze both.
Changes do not reset particle age. Position and tint affect presentation in all modes,
including VFXGraph, without modifying the prefab or particle data. Tint channels must be
finite and in [0, 1], and multiply the final RGB and alpha. A white tint restores normal output.

State and feature queries:

- `isAlive`: the backend has live particles or pending emission. VFXGraph state/counts can lag.
- `isPlaying`: there is an active run, it is not paused, and isAlive is true. Speed zero is still a running state.
- `isPaused`: explicit Pause state; a paused effect can remain alive.
- `capabilities`: ParticleCapabilities flags for the initialized backend, None before initialization.
  All backends support Pause, StopEmitting (VFXGraph requires OnStop handling), SimulationSpeed,
  Position, Tint and Scale. Native and GPU also support ParticleAccess, Emit, EmitIn
  and CoordinateConversion. Query again after SetPrefab or backend recreation.

Run **Tools > Checks > ME.UITK.Particles > Run Controls API Checks** for transformed-button
emission, repeated bursts, speed changes, state and argument checks. Run Color Checks
for final screen tint comparison between Mesh, jobs and RenderTexture.

## Advanced emission, simulation and async playback

These APIs operate on the main/UI thread after attachment and layout. `button` and `counter` are VisualElements on the same panel as `particles`.

```csharp
particles.SetRandomSeed(42u);
particles.Prewarm();
particles.EmitAt(button, new Vector2(.5f, .5f), 16u);
particles.EmitAlongBorder(button, 32u, inset: 2f, roundedCorners: true);
particles.SetAttractor(counter, strength: 250f, captureRadius: 8f, killOnArrival: true);
// Later:
particles.ClearAttractor();
```

| API | Contract |
| --- | --- |
| `EmitAt(VisualElement target, Vector2 normalizedPosition, uint count, int systemIndex = 0)` | One-shot burst at a point of the target border box. Each coordinate must be in [0, 1]; (0,0) is top-left, (.5,.5) is center. |
| `EmitAlongBorder(VisualElement target, uint count, float inset = 0f, bool roundedCorners = true, int systemIndex = 0)` | One-shot burst along the current transformed perimeter. Nonnegative inset is measured in target-local UI units and must leave a positive rectangle. Samples are uniform by transformed segment length. |
| `SetAttractor(VisualElement target, float strength, float captureRadius = 0f, bool killOnArrival = false, int systemIndex = 0)` | Accelerate one system toward the moving target center in the UI plane, preserving depth. Strength is finite nonnegative UI units/second squared; radius is finite nonnegative UI units in ParticleElement. |
| `ClearAttractor()` | Remove attraction without modifying current velocities. |
| `SetRandomSeed(uint seed)` / `randomSeed` | Configure/read a nonzero seed for the next Play or new stopped-to-manual run. Null initially retains the prefab policy. Does not alter current particles. |
| `Prewarm()` | Initialize backend resources and prepare presentation without starting playback. Requires an attached panel and prefab. Does not advance an existing run. |
| `automaticSimulation` | Read/write, default true. Disable to take ownership of simulation time. |
| `Simulate(float deltaTime)` | Advance an initialized active manual run by finite nonnegative seconds. Zero and paused runs do not advance. |
| `PlayAsync(CancellationToken cancellationToken = default)` | Restart and return a Task completed by the configured completion conditions. Requires an attached panel and prefab. |

Rounded borders use resolved USS corner radii, normalized to fit the rectangle, with 16 straight segments per quarter circle. Inset reduces each radius. Clipping by ancestors and transparent image pixels do not restrict emission. Geometry is captured at the moment of emission; existing particles do not follow later layout changes. Counts, capacity, paused playback and system indices follow EmitIn.

Attraction follows the target center each update. Detached or unresolved targets suspend attraction. SetPrefab and Dispose clear the target; Play retains it. Acceleration is multiplied by simulationSpeed; Pause and speed zero freeze it. Capture removes particles within the radius when killOnArrival is true; it does not snap particles, test swept intersections or guarantee arrival without overshoot. Attraction reads/writes CPU snapshots every update (including GPU), so use it selectively for large effects.

```csharp
particles.automaticSimulation = false;
particles.Play();
particles.Simulate(1f / 60f);
particles.Pause();
particles.Simulate(1f / 60f); // Paused: no advancement.
particles.Resume();
particles.Simulate(1f / 60f);
particles.automaticSimulation = true;
```

Manual steps apply the prefab simulation speed and simulationSpeed multiplier. Completion time advances by the supplied delta, independently of speed and completion clock selection; automatic ticks do not advance manual completion time. Switching back to automatic preserves elapsed completion time. Do not step automatically from multiple update loops. Seed reproducibility assumes the same backend, inputs and timestep sequence; native and GPU do not share the same random algorithm. GPU resamples its initial templates when the configured seed changes. Seed also resets random placement for EmitIn and EmitAlongBorder on each new run.

Prewarm is resource preparation, not simulation pre-roll, and does not guarantee every later mesh growth, shader variant or graphics-driver allocation has already happened. Some preparation still needs a rendered frame.

```csharp
particles.completionSettings = new ParticleCompletionSettings {
    timeEnabled = true,
    timeGreaterThan = 1f,
    stopOnCompletion = true,
};
try {
    await particles.PlayAsync(cancellationToken);
} catch (System.OperationCanceledException) {
    // The wait was cancelled, or the playback was interrupted.
}
```

Stop, another Play/PlayAsync, SetPrefab, detach and Dispose cancel the pending task. Token cancellation cancels only the wait and leaves playback running; call Stop explicitly to stop it. A token already cancelled does not start playback. Natural completion or configured thresholds complete the task successfully, including stopOnCompletion. Looping effects need a threshold or external cancellation. The existing ParticleCompletedEvent remains available.

Native and GPU advertise `EmitAt`, `EmitAlongBorder`, `Attractor`, `ManualSimulation`, and `RandomSeed` in ParticleCapabilities. These operations throw NotSupportedException for VFXGraph. All backends advertise `AsyncPlayback` and `Prewarm`; VFXGraph async completion is subject to its delayed particle counters, so time thresholds are recommended. The new settings have no UXML/USS aliases.

Run **Tools > Checks > ME.UITK.Particles > Run Advanced API Checks** in Play Mode. The report is `Logs/ParticleUI-Advanced.txt`.

## Burst parameters and UI transfers

`Emit(ParticleEmitParams parameters, uint count, int systemIndex = 0)` overrides only the supplied start values. Every field is nullable: an omitted field keeps the prefab's sampled value. It does not modify the prefab or existing particles.

```csharp
particles.Emit(new ParticleEmitParams {
    position = Vector3.zero,
    velocity = new Vector3(0f, 2f, 0f),
    startColor = Color.green,
    startSize = .3f,
    startLifetime = 1.5f,
}, 12u, systemIndex: 0);
```

| ParticleEmitParams field | Meaning |
| --- | --- |
| `position` | Finite Vector3 in the selected simulation space; explicit position bypasses Shape positioning. |
| `velocity` | Finite Vector3 in simulation units per second. |
| `startColor` | Color with finite channels in [0, 1]; normal lifetime color modules still apply. |
| `startSize` | Finite nonnegative uniform size in simulation units. |
| `startLifetime` | Finite positive lifetime in simulation seconds. |

`EmitTowards(VisualElement from, VisualElement to, uint count, float travelTime, int systemIndex = 0)` creates a straight transfer from the source center to the destination's current center. Both elements must share the particle panel and have valid layout. The source position is captured once; the destination is sampled each update, so moving UI remains a valid target. Emission capacity, manual-run startup and system indexing follow Emit. A positive finite travelTime is required.

```csharp
particles.RegisterCallback<ParticleArrivedEvent>(OnArrived);
particles.EmitTowards(rewardButton, currencyCounter, count: 12u, travelTime: .6f);

void OnArrived(ParticleArrivedEvent evt) {

    if (evt.destination == currencyCounter) {
        displayedCoins += evt.arrivedCount;
        counterLabel.text = displayedCoins.ToString();
    }

}

// Cleanup:
// particles.UnregisterCallback<ParticleArrivedEvent>(OnArrived);
```

Transfers control position and velocity, and extend the initial lifetime to cover the trip even when the prefab has a shorter lifetime. At each update, live tracked particles are placed at the corresponding point on the path; at the deadline they are removed and reported as arrived. Prefab motion modules can affect rendering between updates, so prefer a simple UI burst prefab without collision/kill modules for predictable transfers. External removal, lifetime edits or changes to tracked randomSeed values can cancel individual particles; cancelled particles are not reported as arrived. The transfer API reserves unique randomSeed values among currently live particles to track them. Do not overwrite or duplicate these identifiers through SetParticles.

The transfer clock uses scaled game time during automatic updates (editor time in Edit Mode), or the supplied Simulate delta in manual mode, multiplied by simulationSpeed. It does not multiply by the prefab simulation speed. Pause and speed zero freeze progress. Stop, Play, SetPrefab, detach and Dispose cancel outstanding transfers without arrival events. A detached or unresolved destination cancels its transfer and removes the remaining tracked particles. StopEmitting lets existing transfers finish. CPU snapshot updates add work proportional to the live particles and active transfers.

`ParticleArrivedEvent` bubbles and trickles down from ParticleElement. It is sent after particles are removed, once per arriving burst, or once per update for attractor captures with killOnArrival enabled. Its read-only payload is:

- `destination`: the destination VisualElement (the event's `target` remains ParticleElement).
- `arrivedCount`: uint number of particles that actually arrived/captured, not the original requested count.
- `systemIndex`: selected system index.
- `localPosition`: destination center in ParticleElement-local UI units.

Subscribe before emission. Use UnregisterCallback during cleanup; do not retain pooled event objects. Multiple transfers can produce separate events in one update. Callbacks may restart or dispose the effect; remaining notifications from the cancelled run are discarded. Arrival is distinct from ParticleCompletedEvent, which reports completion of the whole effect. Completion thresholds can stop a run before a transfer arrives.

Native backends and GPU expose `EmitParameters`, `EmitTowards`, `ArrivalEvents` capability flags. VFXGraph rejects parameterized emission and transfers with NotSupportedException. These APIs have no UXML/USS aliases. Run **Tools > Checks > ME.UITK.Particles > Run Transfer API Checks** in Play Mode; report: `Logs/ParticleUI-Transfers.txt`.


### Continuous emission and area binding

`SetEmissionRate(float particlesPerSecond, int systemIndex = 0)` configures an additional stream on top of prefab emission. The rate must be finite and nonnegative; setting it does not start playback. `BindEmissionArea(VisualElement target, int systemIndex = 0)` distributes this stream across the element's current rectangle, following its movement, size and transform. Binding requires valid layout on the same panel.

```csharp
particles.BindEmissionArea(button);
particles.SetEmissionRate(30f);
particles.Play();

// The stream follows the button through layout changes.
particles.SetEmissionRate(0f); // Disables only the additional stream.
particles.UnbindEmissionArea(); // Returns the stream to the system Shape.
```

Read configuration through `emissionRate`, `emissionArea`, `emissionSystemIndex`. Setting either the rate or area selects the shared system index. The stream respects `simulationSpeed`, Pause/Resume and manual `Simulate`; emission happens at the end of each step. Fractional counts carry between steps; capacity overflow is discarded. Detached targets or invalid layout suspend emission without accumulating missed particles. `StopEmitting()` disables the stream until playback starts again. An active stream keeps `isAlive` true, so stop it to allow natural completion. `SetPrefab` and `Dispose` remove the binding. Native and GPU expose `EmissionRate`, `EmissionArea` capabilities; VFXGraph throws `NotSupportedException` when setting a rate or binding an area.

### Capture and restore state

`CaptureState()` returns an opaque `ParticleState` with a `particleCount` property. `RestoreState(ParticleState state)` restores particles, emitter state, pause/play, completion clocks, managed emission and its random generator, bindings, the attractor and active transfers. Scale, tint, seed and manual simulation settings are also preserved.

```csharp
particles.automaticSimulation = false;
particles.Play();
particles.Simulate(.25f);

if (particles.supportsStateCapture == true) {
    var checkpoint = particles.CaptureState();
    particles.Simulate(.5f);
    particles.RestoreState(checkpoint);
    particles.Simulate(.5f); // Replay the step from the checkpoint.
}
```

Check `supportsStateCapture` or the `StateCapture` capability first. Native backends, analytical GPU and standalone spatial Noise support snapshots, including pending burst waves. Analytic snapshots include trails and pending sub-emitters. VFXGraph and other GPU compute-dynamics (such as Collision or Limit Velocity) throw `NotSupportedException` on capture. This is an allocating, in-memory snapshot of one instance; it cannot be serialized or transferred to another element. Recreating the backend, detaching the element or replacing its prefab invalidates it. Moving the native instance, for example during atlas redistribution, also rejects restoration. A checkpoint can be restored repeatedly.

Snapshots do not capture the scene, layout, external objects or prefab module edits. Targets remain references to existing VisualElements. Restore cancels the current `PlayAsync` and presentation transition; event subscriptions remain, previously delivered events are not undone and may fire again during replay. Use manual simulation with unchanged external conditions for reproducible stepping.

In Play Mode, run **Tools > Checks > ME.UITK.Particles > Run State and Emission Checks**; report: `Logs/ParticleUI-State.txt`.


### Emission through UXML and USS

UI Toolkit uses UXML rather than HTML. `emission-rate` (default 0), `emission-system-index` (0), and `emission-area` (empty string) configure additional emission. The area is the exact unique `name` of an element on the same panel, without `#` or CSS selectors. An empty string uses the system Shape. Missing targets or invalid layout suspend the stream without backlog; a replacement with the same name is resolved on the next emission step.

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns="ME.UITK.Particles">
    <ui:Button name="reward-button" text="Reward" />
    <meparticles name="reward-fx" class="reward-fx"
        prefab="project://database/Assets/Effects/Reward.prefab"
        render-mode="Mesh" auto-play="true"
        emission-rate="20" emission-system-index="0"
        emission-area="reward-button" />
</ui:UXML>
```

```css
.reward-fx {
    width: 300px;
    height: 200px;
    --meparticles-emission-rate: 30;
    --meparticles-emission-system-index: 0;
    --meparticles-emission-area: "reward-button";
}
.reward-fx.quiet {
    --meparticles-emission-rate: 0;
    --meparticles-emission-area: "";
}
```

Replace the prefab path with your asset and attach the USS to the document. USS overrides UXML; removing a rule restores attribute values without restarting playback. These settings do not use `--meparticles-transition-*` animation. VFXGraph permits only defaults; unsupported configuration logs a `meparticles` error in the Console. `CaptureState` / `RestoreState` remain C# commands because they require an in-memory state object.


### Color, scale, speed and attractors in markup

| UXML | USS (`--meparticles-` prefix) | Default |
|---|---|---|
| `tint` | `tint` | white |
| `visual-scale` | `visual-scale` | 1 |
| `system-scale` | `system-scale-x/y/z` | 1,1,1 |
| `position` | `position-x/y` | 0,0 |
| `simulation-speed` | `simulation-speed` | 1 |
| `attractor-target` | `attractor-target` | "" |
| `attractor-strength` | `attractor-strength` | 0 |
| `attractor-radius` | `attractor-radius` | 0 |
| `attractor-kill-on-arrival` | `attractor-kill-on-arrival` | false |
| `attractor-system-index` | `attractor-system-index` | 0 |
| `random-seed` | — | "" |
| `automatic-simulation` | — | true |

```xml
<meparticles name="reward-fx" class="reward-fx"
    prefab="project://database/Assets/Effects/Reward.prefab"
    tint="#80C0FFFF" visual-scale="1.2" system-scale="1,1,1"
    position="0,-8" simulation-speed="1"
    attractor-target="currency-counter" attractor-strength="80"
    attractor-radius="8" attractor-kill-on-arrival="true"
    attractor-system-index="0" random-seed="42" automatic-simulation="true" />
```

```css
.reward-fx {
    --meparticles-transition-duration: 250ms;
    --meparticles-transition-delay: 0ms;
    --meparticles-transition-timing-function: ease-out;
}
.reward-fx.highlighted {
    --meparticles-tint: rgba(255, 180, 80, 0.8);
    --meparticles-visual-scale: 1.5;
    --meparticles-system-scale-x: 1.2;
    --meparticles-position-y: -12;
    --meparticles-simulation-speed: 1.4;
    --meparticles-attractor-target: "currency-counter";
    --meparticles-attractor-strength: 120;
    --meparticles-attractor-radius: 12;
    --meparticles-attractor-kill-on-arrival: true;
    --meparticles-attractor-system-index: 0;
}
```

`visual-scale` and all `system-scale` components must be finite and strictly positive. Position uses UI units; speed, strength and radius must be finite and nonnegative. Tint accepts USS colors (`#hex`, `rgb`, `rgba`, named colors) with channels in 0–1; UXML uses Unity color syntax such as `#80C0FFFF`.

Transitions now include tint, both scales, position and speed alongside pixels-per-unit and VFXGraph bounds. Initial application and backend replacement apply immediately. Removing a class transitions back to UXML defaults; interruption starts from the current interpolated state. Transitions use unscaled time and also run on stopped effects. Attractor settings change immediately.

The attractor target is a unique `name` on the same panel, without a CSS selector. Empty names disable attraction; missing targets or invalid geometry suspend it. A replacement with the same name is resolved on the next step. Strength uses UI units/s², radius uses UI units. VFXGraph supports presentation controls and speed, but rejects attractors, explicit seeds and manual simulation with a Console error.

`random-seed` is UXML-only: empty strings use prefab policy, integers 1…4294967295 set the seed for the next Play. Changes do not restart the current effect. `automatic-simulation="false"` is also UXML-only; advance the started effect through C# `Simulate(deltaTime)` calls. Corresponding C# setters are `uxmlTint`, `uxmlVisualScale`, `uxmlSystemScale`, `uxmlPosition`, `uxmlSimulationSpeed`, `uxmlAttractorTarget`, `uxmlAttractorStrength`, `uxmlAttractorRadius`, `uxmlAttractorKillOnArrival`, `uxmlAttractorSystemIndex`, `uxmlRandomSeed` (string) and `uxmlAutomaticSimulation`. Call `ApplyUxmlSettings()` after batching changes to apply them immediately.


## VisualElement reveal effects

`ParticleReveal` reveals an existing element with procedural UI particles. `Cloud` emits
spark quads while fading in the element. `Assemble` flies textured fragments into place,
then hands presentation back to the live UI. These effects use UITK geometry directly;
they do not require a ParticleSystem prefab or select a `ParticleRenderMode`.

```csharp
// Run after the target is attached and has a positive layout.
var reveal = ParticleReveal.Cloud(target, duration: .8f, count: 100u);
// Cancel early and restore the original inline opacity:
reveal.Dispose();

// CreateRevealContent must return a NEW subtree with the same appearance as target.
// Include the necessary style sheets, classes, text and data in that factory.
var snapshot = await ParticleRevealSnapshot.CaptureAsync(
    CreateRevealContent, document.panelSettings, target.layout.size, cancellationToken);
reveal = ParticleReveal.Assemble(target, snapshot, duration: 1.2f, columns: 32u);
// Keep snapshot alive for the entire effect; cache it for subsequent reveals.
// On owner teardown: reveal.Dispose(); UnityEngine.Object.Destroy(snapshot);
```

- Duration uses unscaled real time and must be finite and positive. Spread is in target
  local UI units, finite and nonnegative. Cloud supports 1–4096 particles; Assemble
  supports 1–128 columns and derives up to 128 rows from the target aspect ratio.
- The target keeps its layout and interaction state. Effects temporarily own its opacity;
  avoid simultaneous opacity animations or style edits. Completion, `Dispose`, detach
  and replacement by another reveal restore the original inline opacity, including USS
  inheritance. Keep a handle and dispose it when the owning UI is closed.
- The overlay is a non-picking sibling immediately above the target. Target transforms
  and dimensions are followed each frame. Ancestor clipping still applies; the target's
  own overflow mask does not clip scattered particles. Put it in a wrapper to clip them.
- Assembly requires a texture of the target's local border box with straight alpha.
  Texture ownership stays with the caller; imported textures need not be readable.
- `CaptureAsync` requires Play Mode and the Unity main thread. It renders a fresh subtree
  to a transparent offscreen panel using a copy of the supplied panel settings and waits
  three rendered frames. Dimensions are limited to 2048 per axis. It performs synchronous
  pixel readback once; prepare/cache snapshots before frequent animations.
- Capture does **not** automatically clone an arbitrary live VisualElement. Inherited
  ancestor selectors, bound data, hover/focus states and live custom drawing must be
  reproduced by the factory. The original element is never reparented. Snapshots remain
  static if text or styling changes; regenerate them when needed. Cancel pending capture
  on owner teardown, and keep Game View rendering in the Editor.

The playground sidebar includes **Visual Element Reveal**, with **Cloud reveal** and
**Assemble UI** buttons. Host validation: **Tools > Checks > ME.UITK.Particles > Run Reveal
Checks**, in Play Mode with Game View visible; report: `Logs/ParticleUI-Reveal.txt`.
### Rendering above UI and clipping

`SetRenderOnTop(bool enabled = true)` enables the top rendering layer on the current panel. The read-only `renderOnTop` property reports its state; disabled by default.

```csharp
particles.SetRenderOnTop(true);
particles.EmitIn(button, 24u);

// Restore normal rendering without restarting the effect.
particles.SetRenderOnTop(false);
```

Use `render-on-top="true"` in UXML or `--meparticles-render-on-top: true;` in USS. C# markup configuration is available through `uxmlRenderOnTop` and `ApplyUxmlSettings()`. This setting applies immediately without interpolation.

The effect bypasses clipping on itself and its ancestors, including ScrollView and rounded borders, and draws above other elements on the same panel. ParticleElement stays in its original hierarchy, retaining layout, coordinate APIs, scrolling position and simulation. Ancestor opacity, visibility and display still apply; the layer ignores input. Multiple effects share a layer in insertion order. Supports Mesh/Jobs, RenderTexture, Atlas, GPU, Auto and VFXGraph. Detach and Dispose remove helper elements; reattachment restores an enabled overlay. State snapshots preserve this flag.

Screen/RenderTexture boundaries and ordering between separate panels or UIDocuments are unchanged. Configure Unity panel ordering to display above another panel.


### Reveal settings in USS

```css
.reward {
    --meparticles-reveal-mode: assemble;
    --meparticles-reveal-duration: 1.2;
    --meparticles-reveal-spread: 100;
    --meparticles-reveal-columns: 32;
    --meparticles-reveal-count: 100;
    --meparticles-reveal-color: rgb(80, 220, 255);
}
```

Settings are read from the target when playback starts, after styles resolve.
`ParticleReveal.Play(target, snapshot)` selects `cloud` or `assemble` using the mode
property. `Cloud` and `Assemble` select their own mode but also read USS settings.
Precedence: explicit C# argument → USS → default. Duration is unitless seconds;
spread is unitless local UI distance. Color and count apply to Cloud, columns to Assemble.
Style/class changes affect the next invocation, without restarting a running effect.
USS alone does not start playback: call Play after layout/style resolution. Assembly
still requires a snapshot supplied from C#.


### Hiding

Use `ParticleReveal.Cloud(target, hide: true)` or
`ParticleReveal.Assemble(target, snapshot, hide: true)` to fade out with sparks or
scatter the UI fragments. USS: `--meparticles-reveal-direction: hide;` (default: `show`).
`Play` reads this direction; an explicit `hide: false` selects appearance.

Successful hiding leaves `opacity: 0`, preserving layout and interaction. It does not
set `display: none`; manage input separately if needed. A subsequent appearance restores
the opacity saved before hiding, including its original inline/USS value. Cancellation
or detach during playback restores the state at invocation. Disposal after completion
does not undo hiding. Avoid manual opacity changes between paired hide/show calls: the
saved opacity is retained until a successful appearance. The demo includes **Hide cloud**
and **Scatter UI** buttons.
