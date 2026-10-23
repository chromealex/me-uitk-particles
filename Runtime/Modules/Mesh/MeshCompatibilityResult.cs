// <summary>
// Defines the reasons a particle prefab cannot use the UI Toolkit mesh rendering path.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

namespace ME.UITK.Particles {

    /// <summary>The first reason a prefab cannot use the UI mesh rendering path.</summary>
    public enum MeshCompatibilityResult {
        Compatible = 0,
        MissingPrefab,
        NoParticleSystem,
        UnsupportedSimulationSpace,
        TrailsEnabled,
        LightsEnabled,
        TextureSheetAnimationEnabled,
        CustomDataEnabled,
        MissingRenderer,
        UnsupportedRenderMode,
        MissingMaterial,
        MissingShader,
        UnsupportedShader,
        MaterialUvTransform,
    }

}
