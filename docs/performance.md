# Performance measurement

Measured in the Unity 6000.6.0f1 editor on macOS with URP 17.6.0, using the included
Confetti preset in the overlay and world-space panels. These are local observations,
not target-device benchmarks or a guarantee for arbitrary particle prefabs.

## Current result

```text
GC.Alloc calibration: 1 allocation events for new byte[4096]
World setup (2 elements + demo UI): 85 allocation events, 1.034 ms
Overlay setup (2 elements + demo UI): 83 allocation events, 0.474 ms
RT PrepareTextureFrame: 0.0 allocation events/call, 0.0010 ms/call (32 warmed direct calls)
RT PresentTextureFrame: 0.0 allocation events/call, 0.0026 ms/call (32 warmed direct calls)
RT Play: 0.0 allocation events/call, 0.0004 ms/call (32 warmed direct calls)
RT PrepareTextureFrame: 0.0 allocation events/call, 0.0012 ms/call (32 warmed direct calls)
RT PresentTextureFrame: 0.0 allocation events/call, 0.0026 ms/call (32 warmed direct calls)
RT Play: 0.0 allocation events/call, 0.0004 ms/call (32 warmed direct calls)
Mesh DrawParticles: 0 allocation events / 174 actual draws, 0.1317 ms/draw
Mesh DrawParticles: 0 allocation events / 172 actual draws, 0.1198 ms/draw
```

The `GC.Alloc` recorder counts allocation events, not bytes. Its calibration observed
one event for a known allocation. It is reset for each actual Mesh drawing sample
and each warmed RT method call, excluding unrelated Editor work. RT presentation
is given a completed frame before every measured call so the blit actually runs.

No managed allocation events were observed in the measured steady-state methods.
Setup still allocated. Native memory, GPU work, initial buffer growth, and the rest
of the Unity rendering pipeline are outside these counters.

## CPU comparison

Before the changes, actual Mesh drawing samples averaged approximately
0.19–0.22 ms per draw. The current run is approximately 0.12–0.13 ms per draw.
Editor load and frame scheduling can vary; treat these numbers as indicative.
The baseline allocation-byte readings were discarded because the Mono byte counter
returned zero for known allocations; no before/after byte reduction is claimed.

## Changes

- Reused unique Mesh vertices and original indices; retained chunking for large meshes.
- Converted repeated vertex colors once per color run instead of per triangle corner.
- Preallocated scratch buffers and used geometric growth beyond the warm capacity.
- Removed unused bake meshes in RT mode.
- Rounded RT growth to 64-pixel blocks, retained capacity, and avoided resolving the same frame twice.
- Avoided unchanged UI style writes and paused scheduled updates on explicit Stop.
- Cached non-sub-emitter playback systems and removed recursive/double startup.
- Pooled compatibility-query scratch lists and removed params arrays from bounds sampling.
- Updated demo status text only when changed, at most ten times per second.

Creation, attach/recreation, preset replacement, buffer/texture growth, and changed
demo text still allocate. Buffer capacity retention uses more memory to avoid later
allocations. Prefab instances and cameras are not pooled. Bounds pre-simulation
still runs synchronously at creation.

Run **Tools → Checks → ME.UITK.Particles → Run Performance Checks** in the playground's
Play Mode to produce a fresh `Logs/ParticleUI-Performance.txt`.

## Stress scene and worker jobs

`Demo/Stress/ParticleStressTest.unity` provides 1,000 to 50,000 particles across 1, 4, or 16 elements, with Auto/Mesh/RT selection and a large-particle overdraw option. Its 10-second measurement follows a 2-second warm-up and freezes the HUD. Its GC counter covers the whole frame, unlike the scoped benchmark above.

On Unity 6000.6.0f1, the automated stress checks passed 18 configurations: 1k/5k/10k/25k/50k in one element and 16k across 16 elements, each in Auto with jobs enabled, Auto with jobs disabled, and RT. Checks cover exact live counts, resolved mode, generated vertex counts and clearing. The 25k and 50k cases exercise triangle chunking beyond UITK's ushort index limit. Jobs still use the synchronous path below 4,096 vertices per renderer.

Large Mesh workloads can convert vertex positions and colors on worker threads using `MeshGenerationContext.AddMeshGenerationJob`. `useJobs` defaults to true, with a configurable `jobVertexThreshold` of 4,096. Main-thread baking, data copying and submission remain. UITK owns the temporary mesh buffers and completes registered jobs before consuming them. `ParticleVertexJob` is compiled with Burst. This is not a claim of a measured speedup, or of zero allocations for the new jobs path: compare both modes and profile all threads in a Player build.

Color regression checks also passed all 16 cases: 8 synchronous and 8 forced through jobs, compared against RT screen pixels with the existing 3/255 tolerance. The tilted world-space panel was checked in Play Mode, including clicking its emission button.

The extracted `ParticleVertexJob` now uses Burst. Native ARM64 output was verified in Burst Inspector on Unity 6000.6.0f1, and all 16 screen-color checks passed with Burst compilation enabled. The completion API now dispatches pooled `ParticleCompletedEvent` events; 23 checks passed, including trickle-down, bubbling and unregistration. First-use event pool growth is outside the zero-allocation claim.
