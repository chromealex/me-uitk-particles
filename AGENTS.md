# ME.UITK.Particles: Agent Guide

## Purpose and sources

ME.UITK.Particles displays ParticleSystem and VFX Graph effects inside UI Toolkit
through `ParticleElement`. It is a `VisualElement`, not a MonoBehaviour. The public
namespace and runtime assembly are `ME.UITK.Particles`. Overlay and world-space
UIDocuments are supported.

- Main implementation: `Runtime/`.
- API contract: `ParticleElement.cs`, `ParticleRenderMode.cs`,
  `ParticleCompletionSettings.cs`, and `ParticleCompletedEvent.cs` in `Runtime/Core/`.
- Detailed guide: [README.md](README.md).
- Examples: `Demo/ParticleDemo.cs` and `Demo/ParticleStressTest.cs`.
- If documentation and implementation disagree, verify behavior against the code.
  Do not invent public methods, UXML attributes, or Inspector settings that the
  implementation does not provide.

## Project setup

Paths in this file are relative to the library repository root unless stated otherwise.
To run the demos, place the library at `Assets/ME.UITK.Particles` in a Unity project:
scene generators use this path.

The verified configuration is Unity **6000.6.0f1**. In the host Unity project, check
`ProjectSettings/ProjectVersion.txt` for the editor version and `Packages/manifest.json`
for dependencies; these files are not part of the library repository. The configuration
uses URP **17.6.0**, Visual Effect Graph **17.6.0**, Burst **2.0.0**, Input System
**1.20.0**, and uGUI **2.6.0**. Do not treat other versions or render pipelines as verified.

- This is an asset folder, not a UPM package. When moving it, preserve the entire
  `Runtime` folder, including shaders, `Resources`, asmdef, and `.meta` files.
  The runtime uses Burst and Collections.
- Add a reference to `ME.UITK.Particles` in the consuming code's asmdef.
- Set up a `UIDocument` with Panel Settings. The demos require Input System to be enabled.
- Reserve layer **31** for the adapter and exclude it from scene cameras.
- A backend requires an attached panel and an assigned prefab. Runtime panels require
  Play Mode; editor panels support Edit Mode through Editor/ParticleEditorLifecycle.cs.
  Use Tools > Checks > ME.UITK.Particles > Run Editor Checks to validate Edit Mode rendering.

## Minimal API integration

Create and configure the element in C#. Give it nonzero dimensions through layout or styles.
Prefer assigning settings and the prefab before adding it to an attached visual tree.
In this snippet, `document` is a configured UIDocument and `effectPrefab` is a GameObject
with a ParticleSystem on its root or a child:

```csharp
using ME.UITK.Particles;
using UnityEngine.UIElements;

var particles = new ParticleElement {
    pixelsPerUnit = 60f,
    useJobs = true,
    jobVertexThreshold = 4096u,
};
particles.style.width = 300;
particles.style.height = 220;
particles.SetPrefab(effectPrefab, ParticleRenderMode.Auto);
document.rootVisualElement.Add(particles);
particles.Play();

// When permanently removing the UI:
particles.Dispose();
particles.RemoveFromHierarchy();
```

The prefab origin maps to the center of `contentRect`. Increasing `pixelsPerUnit`
makes the effect larger. It specifies UI units per simulation unit.

## Choosing a rendering mode

Pass the mode as the second argument to `SetPrefab`. To change modes, assign the
prefab again and call `Play()`. Do not attempt to assign `activeMode`.
`ParticleRenderMode` is a flags mask: Auto = GPU | Mesh | RenderTexture | RenderTextureAtlas.
Use bitwise OR for a subset, or `Auto & ~RenderTextureAtlas` to exclude Atlas.
Excluded modes must never be used as fallbacks. VFXGraph is standalone and cannot be combined.
Empty/unknown masks are rejected; incompatible subsets preserve the current effect.
UXML accepts comma-separated names. Atlas without RT uses a page even for one effect;
if size/device limits prevent it, drawing pauses until eligible again. Numeric enum values
changed (Auto 0 -> 15, VFXGraph 3 -> 16, Atlas 5 -> 8); external numeric data needs explicit migration.

| Mode | Behavior and use |
| --- | --- |
| `Auto` | Checks GPU first, then compares Mesh and RenderTexture frame time for Mesh-compatible effects. Eligible RT candidates, including measured RT winners, can automatically join a RenderTextureAtlas. VFX Graph is never selected automatically. |
| `Mesh` | Native simulation and BakeMesh, with geometry submitted to UITK. Explicit Mesh rejects incompatible prefabs before replacing the current effect and never switches rendering paths. |
| `RenderTexture` | Native rendering through an internal camera and a transparent texture. Use for effects that fail Mesh compatibility checks. |
| `RenderTextureAtlas` | Compatible effects share a camera and textures. Incompatible Atlas-only prefabs are rejected. Oversized tiles use RenderTexture only if its flag is permitted; otherwise rendering is suspended with activeMode None. |
| `GPU` | Analytic simulation of a limited subset of ParticleSystem settings in a shader. Explicitly requesting this mode for an incompatible prefab throws an exception. |
| `VFXGraph` | Explicit VFX Graph mode: the prefab contains VisualEffect components with assigned graphs and no ParticleSystem components. Requires compute shaders and a compatible device. |

Mesh requires local simulation space, a billboard renderer, and a material using
`ParticleUI/Alpha` with no UV scale/offset. Trails use BakeTrailsMesh and require a
compatible trail material too. Grid and Sprites Texture Sheet Animation use baked UVs; sprites must be Full Rect and share one texture. Lights and Custom Data are incompatible with this path. The entire ParticleSystem hierarchy
is checked.

GPU accepts an active root ParticleSystem with up to eight direct Birth/Death sub-emitters: local space, no loop/prewarm,
one or more guaranteed single-cycle bursts within Duration, totaling up to 16000 particles without exceeding maxParticles,
and no continuous emission. It supports a disabled Shape, a limited Circle configuration or transformed Box volume,
the `ParticleUI/Alpha` material, and selected color, size, and gravity settings.
Motion supports Z Rotation over Lifetime, linear Local Velocity over Lifetime,
Local Force over Lifetime without Randomize, and approximate GPU position Noise.
Noise without Trails or parent Sub Emitters uses a shared spatial GPU curl field. Other Noise systems retain analytic waves. Neither path promises exact Unity trajectories. Standalone spatial Noise supports checkpoints; other compute dynamics still reject them. Run GPU Spatial Noise Checks for field and checkpoint changes.
Burst scheduling preserves pending waves across checkpoints. StopEmitting cancels future waves. Run GPU Box and Waves Checks for Box shape and emission schedules.
See the README module table. Run GPU Module Checks for motion changes.
See `Runtime/Modules/GPU/GpuParticleCompatibility.cs` for the complete restrictions.
Trails support analytic Per Particle strips in local space with constant lifetime and Stretch UVs.
Sub Emitters support Birth/Death and Color/Size/Rotation inheritance, without nesting.
Children require identity local transforms and the root simulation clock. Particle APIs
accept child systemIndex in hierarchy order. Natural completion waits for children and
surviving trails. Run GPU Hierarchy Checks for changes; see the README for limits.
Nonuniform Main Start Size supports constant/two-constant XYZ: X/Y size the billboard,
Z is retained by particle APIs. Start rotation and rotation modules remain Z-only.
Run GPU Rotation Size Checks after changing size/rotation packing.
Appearance also supports Color/Size/Rotation by Speed and Grid/Sprites Texture Sheet Animation. Sprites require Full Rect geometry, no tight packing, and a shared texture. Run Texture Sheet Checks for final UI colors in Mesh (with/without Jobs), GPU and RT.
Custom emitter velocity supports Inherit Velocity and Lifetime by Emitter Speed.
Limit Velocity, Plane Collision, spherical Kill/Ignore Triggers and listed spherical wind
use GPU compute, never native particle simulation. See GpuParticleDynamics.Check for
specific restrictions. Stateful dynamics cannot share a system with Trails or parent
Sub Emitters. Counts are delayed; particle edits/read APIs synchronize GPU state.
Lights are UI glow, not scene lights. Custom Data has a read API, not custom material
vertex streams. Run Extended GPU Checks and existing motion/hierarchy checks.
Do not promise Unity's exact random sequence, full module settings, or Physics parity.

For compatibility checks before creating an element, use:

```csharp
var meshResult = ParticleElement.MeshCompatibility(effectPrefab);
var shaderReason = ParticleElement.GpuCompatibility(effectPrefab);
// Mesh: MeshCompatibilityResult.Compatible means compatible.
// GPU: null means compatible; a string explains the rejection.
```

In Auto, Atlas groups four playing, compatible RT candidates with matching tile parameters.
The group remains active down to two effects; a single remaining effect returns to RT
without restarting simulation. GPU retains priority. Mesh-compatible effects compare Mesh/RT on the same simulation, starting with Mesh and preferring it within a 5% difference. RT winners can join Atlas after calibration; Atlas uses group-size heuristics, not a separate timing comparison. Tiles are limited to 1024 pixels
per axis and 128 world units; support for 2048 textures is required. This optimizes cameras
and compositing, not particle simulation cost.

## Settings and diagnostics

| Member | Default and meaning |
| --- | --- |
| `pixelsPerUnit` | `65f`, effect scale. Use a finite positive value. |
| `useJobs` | `true`, transforms large Mesh buffers through Job System/Burst. Does not affect VFXGraph or RT. |
| `jobVertexThreshold` | `4096u`, minimum vertex count for jobs. Counts vertices, not particles. |
| `gpuBounds` | Bounds centered at `Vector3.zero` with size `(10, 10, 10)`. Defines the GPU camera volume relative to the instance; the center must be finite and dimensions finite and strictly positive. |
| `completionSettings` | All thresholds disabled, natural completion; `Any`, `Unscaled`, `stopOnCompletion = false`. Settings are validated on assignment and captured at `Play()`. |
| `hasInstance` | Whether a backend exists. Does not guarantee a native ParticleSystem exists: GPU does not create one. |
| `activeMode` | Actual mode of the created backend; check alongside `hasInstance`. Can change during Atlas regrouping. |
| `compatibilityInfo` | Explains mode selection in Editor/DEBUG. Other builds return a placeholder; do not use it for application logic. |
| `particleCount` | `uint`, live particles. VFXGraph counts arrive with a delay through Unity's asynchronous readback. |
| `bakedVertexCount` | `uint`, vertices from the last Mesh draw; always 0 for GPU/VFXGraph. |
| `renderBounds` | Capture bounds relative to the instance origin; use to diagnose texture-based paths. |
| `hasCompletedPlayback` | Indicates that completion has been reported. `Play()` and `SetPrefab()` reset it. Does not indicate that simulation has stopped. |

Configure the particles themselves on the prefab through ParticleSystem modules or
VFX Graph. After changing the prefab, recreate the effect with `SetPrefab()` and `Play()`.
Do not access internal backends or helper objects from consuming code.

For VFXGraph, set suitable `gpuBounds` before playback; configure the VFX Graph's own culling
bounds separately. Prefer local-space graphs with transparent alpha blending.
Use a time threshold for reliable VFXGraph completion: an empty or very short effect may
be missed by asynchronous particle counting and never report natural completion.

## Playback and resource ownership

- `SetPrefab(prefab, mode)` releases the previous backend, cancels pending completion,
  and resets the intent to play. Call `Play()` explicitly after replacement.
  `SetPrefab(null)` clears the assignment.
- `Play()` clears and restarts the effect. It does not pause/resume or add an independent
  burst. Simultaneous independent effects require separate elements.
- Calling `Play()` before attachment records the intent to play; actual playback waits
  for a panel.
- `Stop()` immediately stops and clears the effect, cancels the pending completion event,
  and pauses scheduled updates. Resources remain available for another run.
- Detach releases resources while preserving the prefab and intent to play. Attach
  recreates the effect and starts it from the beginning if it was playing.
- `Dispose()` releases resources and clears the prefab and intent to play. The element
  remains in the tree: call `RemoveFromHierarchy()` to remove it.
- UITK subscriptions are not automatically removed by Dispose. Unsubscribe external
  listeners when no longer needed. Manual Stop, SetPrefab, detach, and Dispose do not
  dispatch `ParticleCompletedEvent`.

## Completion event

Subscribe before `Play()`. Without thresholds, completion waits for all systems to finish
naturally; a looping effect does not finish on its own. Example using time and particle count:

```csharp
particles.completionSettings = new ParticleCompletionSettings {
    timeEnabled = true,
    timeGreaterThan = 2f,
    countEnabled = true,
    countLessThan = 5u,
    match = ParticleCompletionMatch.All,
    clock = ParticleCompletionClock.Unscaled,
    stopOnCompletion = true,
};
particles.RegisterCallback<ParticleCompletedEvent>(OnParticlesCompleted);
particles.Play();
// When removing the listener:
// particles.UnregisterCallback<ParticleCompletedEvent>(OnParticlesCompleted);
```

`OnParticlesCompleted` is a listener method with the signature
`private void OnParticlesCompleted(ParticleCompletedEvent evt)`.

- Comparisons are strict: time `> timeGreaterThan`, count `< countLessThan`.
  Time must be finite and nonnegative; `countLessThan >= 1`.
  To check for zero particles, use `countLessThan = 1u`.
- `All` requires every enabled condition; `Any` requires at least one. Threshold conditions
  replace the natural completion check. Disable count to check time only.
- The initial zero before particles appear is ignored until particles have been observed
  or natural completion is established. A gap between bursts can satisfy the count threshold.
- `Unscaled` measures Unity's unscaled time; `Scaled` measures game time. Timing starts
  at actual playback, not element creation; it is not ParticleSystem simulation time.
- The event is dispatched once per run on the main thread during a scheduled update.
  `stopOnCompletion = true` stops and clears the effect before dispatch; when `false`,
  the notification itself does not stop emission or rendering.
- Calling Play, SetPrefab, or Dispose in the handler is supported. The event supports
  bubbling and trickle-down: `evt.target` is the source element, and `evt.currentTarget`
  is the listener. Do not retain the pooled event instance after the handler returns.

## UI layering and clipping

To overlay an effect, stretch ParticleElement over the target container using
`Position.Absolute` and `left/right/top/bottom = 0`. Use `PlaceBehind(button)` or
`PlaceInFront(button)`; the particle element and button must share a parent.
Visual tree order determines layering for both Mesh and texture-based paths.

The particle element and its texture child use `PickingMode.Ignore`.
Set `Overflow.Hidden` on the container to clip the effect. Check every ancestor for
clipping: `Overflow.Visible` on ParticleElement does not override a mask higher in the tree.

## Limitations and troubleshooting

- If the effect is invisible, check panel context (runtime requires Play Mode), panel attachment, element dimensions,
  the prefab, Play after SetPrefab, `hasInstance`, `activeMode`, and `compatibilityInfo`.
- For unexpected RT selection, check material, simulation space, and module compatibility.
  Do not disable fallback merely to obtain an expected mode name.
- For clipping, check UI clipping; for VFXGraph, check gpuBounds and graph bounds.
  RT bounds are estimated through presimulation and expanded at runtime.
  Arbitrary shader vertex displacement may not be accounted for.
- RT is limited to 1024 pixels per side, so large effects may look blurry.
  Presimulation is synchronous and limited to 15 seconds / 450 steps;
  heavy prefabs may cause creation spikes. RT presentation has roughly one frame of latency.
- Native instances are placed far from the scene on layer 31. Prefab scripts run;
  collision, triggers, and external forces do not automatically inherit the original
  scene environment. The camera looks along Z at the XY plane, and prefab root rotation
  is reset.
- Do not promise identical compositing for additive, lit, distortion, or soft-particle
  materials. Pure additive output with zero alpha is unsuitable for standard transparent
  UI texture presentation.
- Do not promise zero allocations across the entire lifecycle. Resource creation,
  buffer growth, first playback, and user scripts need separate profiling.
  Details: [performance.md](docs/performance.md).

## Demo and framework menu

The library owns **Tools > ME.UITK.Particles**. Its commands and implementations
stay in this library's `Editor/` folder:

- `Open Demo` opens the existing `Demo/ParticlePlayground.unity` without regeneration.
- `Build and Open Demo` regenerates the playground scene and `Demo/Generated` assets.
- `Update Demo Presets` regenerates the preset prefabs, updates the playground's
  preset references, and saves the scene.
- `Build and Open Stress Test` regenerates the stress-test scene and its assets.
- `Create VFXGraph Sample` creates missing `Demo/VFXGraph/VFXGraph Burst.vfx` and its prefab
  from Unity's Simple Burst template; existing files are preserved.
- `Editor Preview` opens an editor window for previewing a prefab and rendering mode.

Scene and asset paths above are relative to this library. Builders expect it at
`Assets/ME.UITK.Particles` in the host project. Regeneration can overwrite manual
changes to scenes and generated assets; use `Open Demo` when only opening the demo.
Keep checks and development utilities outside this library and this menu;
the host project's AGENTS.md documents their locations and commands.

## Validating changes

After C# changes, verify compilation in the host project's Unity version.
For documentation-only changes, verify examples against the API.
Run rendering checks in Play Mode with Game View available rather than relying
solely on headless execution. Follow the host project's local AGENTS.md for
validation scripts, menu commands, reports, and check selection.
Keep host-project checks and development utilities outside this library;
library and demo code must not depend on their assembly.
State which checks were actually performed in the final response.

## Per-particle access

`ParticleElement.GetParticles` / `SetParticles` accept reusable `ParticleSystem.Particle[]`
buffers with optional size, offset and systemIndex. Check `supportsParticleAccess`;
Mesh, RT, Atlas and GPU support access, VFXGraph does not. GPU exposes root and registered sub-emitter local-space
systems and evaluates analytic state at the last rendered time, with edits uploaded to
the mesh. Play restores its original seeds. GPU supports position, velocity, lifetime,
color, uniform or nonuniform billboard size, 2D rotation and angular velocity; other Particle fields do not enable
additional shader features. See the README for limits. Run Particle Access Checks for
native paths and Run GPU Checks for analytic editing and rendered color verification.

## Declarative UI

`ParticleElement` is a Unity 6 `[UxmlElement("meparticles")]` with UXML attributes in
`Runtime/Core/ParticleElement.Uxml.cs`. Declare `xmlns="ME.UITK.Particles"` for the short
tag. Prefabs are UXML GameObject asset references. Scalar USS settings use the
`--meparticles-` prefix; bounds use individual x/y/z properties. USS overrides UXML,
removing a rule restores UXML defaults. Settings are batched after attachment; completion
settings are captured on Play. Run **Run Markup Checks** after markup changes and inspect
`Logs/ParticleUI-Markup.txt`. See the README UXML/USS section and Demo/ParticleMarkup.uxml.

Demo layouts live in Demo/ParticleDemo.uxml and Demo/ParticleStressTest.uxml; dynamic
particles use ParticleClick.uxml and ParticleStressCell.uxml. Preserve serialized layout
and template references in scenes and builders. Use ApplyUxmlSettings before immediate
Play after changing declarative defaults. Custom USS transitions animate pixelsPerUnit
and VFXGraph bounds via --meparticles-transition-duration/delay/timing-function; ordinary
visual transitions are native UITK. Run Markup Checks and Demo Markup Checks for changes.

## Runtime controls and manual emission

ParticleElement exposes simulationSpeed (a nonnegative prefab-speed multiplier),
SetPosition(Vector2), SetTint(Color), isPlaying/isPaused/isAlive and ParticleCapabilities.
Emit(count, systemIndex) and EmitIn(VisualElement, count, systemIndex) support native
backends and GPU; VFXGraph throws NotSupportedException. EmitIn samples the target's
transformed rectangular border box on the same panel after layout, not rounded clipping.
Manual emission starts stopped effects without automatic prefab emission and preserves
existing/paused particles. Read README for capacity and completion behavior. Run Controls
API Checks and Color Checks for these paths.
