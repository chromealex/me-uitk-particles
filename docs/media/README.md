# Demo recordings

These GIFs show the actual `ParticlePlayground` scene in Unity 6000.6.0f1 / URP 17.6.0.
No particle animation was synthesized for the documentation.

| File | Recorded scenario |
| --- | --- |
| `overview.gif` | Confetti with clipping; switching from the 2D overlay to the 3D world-space panel. |
| `layering.gif` | Sparks behind an opaque button, then above its background and text. |
| `trails.gif` | Trails across rendering modes in the 2D overlay, above UI with visible overflow. |

## Re-record

1. Run **Tools → ME.UITK.Particles → Open Demo** to open the existing `Assets/ME.UITK.Particles/Demo/ParticlePlayground.unity`.
2. Keep the Game View visible, use approximately 900 × 750 pixels. Enter Play Mode.
3. Run **Tools → Development → ME.UITK.Particles → Capture README Frames**. Keep the editor running and avoid resizing the Game View or changing presets during capture.
4. Wait for `README capture finished` in the Console. The log includes the temporary frame directory. Capture changes the demo's 2D/3D tab, presets/scenarios and disables Repeat; no scene or prefab assets are saved.
5. With Python 3 and Pillow installed, run from the repository root:

   ```sh
   python3 docs/tools/build_gifs.py /path/from/the/unity/console
   ```

The capture requests 40 frames per clip, spaced at least 80 ms apart. The encoder skips the first frame after setup, uses a shared palette, and produces looping GIFs at 80 ms per frame. Editor load can affect capture timing; these are visual demonstrations, not performance benchmarks.

Raw PNGs remain in the temporary directory. Only the optimized GIFs need to be committed.
