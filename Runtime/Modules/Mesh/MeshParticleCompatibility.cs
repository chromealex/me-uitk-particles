using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal static class MeshParticleCompatibility {

        /// <summary>Returns the first incompatibility found in the prefab hierarchy, or Compatible.</summary>
        public static MeshCompatibilityResult MeshCompatibility(GameObject source) {

            if (source == null) {
                return MeshCompatibilityResult.MissingPrefab;
            }

            var systems = UnityEngine.Pool.ListPool<ParticleSystem>.Get();
            try {
                source.GetComponentsInChildren(true, systems);
                return MeshParticleCompatibility.MeshCompatibility(systems);
            } finally {
                UnityEngine.Pool.ListPool<ParticleSystem>.Release(systems);
            }

        }

        private static MeshCompatibilityResult MeshCompatibility(List<ParticleSystem> systems) {

            if (systems.Count == 0) {
                return MeshCompatibilityResult.NoParticleSystem;
            }

            foreach (var system in systems) {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.Local) {
                    return MeshCompatibilityResult.UnsupportedSimulationSpace;
                }

                if (system.lights.enabled == true) {
                    return MeshCompatibilityResult.LightsEnabled;
                }

                if (system.textureSheetAnimation.enabled == true
                    && system.textureSheetAnimation.mode == ParticleSystemAnimationMode.Sprites
                    && ParticleSpriteSheet.Check(system.textureSheetAnimation) != null) {
                    return MeshCompatibilityResult.TextureSheetAnimationEnabled;
                }

                if (system.customData.enabled == true) {
                    return MeshCompatibilityResult.CustomDataEnabled;
                }

                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null) {
                    return MeshCompatibilityResult.MissingRenderer;
                }

                if (renderer.renderMode != ParticleSystemRenderMode.Billboard) {
                    return MeshCompatibilityResult.UnsupportedRenderMode;
                }

                var result = MeshParticleCompatibility.MaterialCompatibility(renderer.sharedMaterial);
                if (result != MeshCompatibilityResult.Compatible) {
                    return result;
                }

                if (system.trails.enabled == true) {
                    result = MeshParticleCompatibility.MaterialCompatibility(renderer.trailMaterial);
                    if (result != MeshCompatibilityResult.Compatible) {
                        return result;
                    }
                }
            }

            return MeshCompatibilityResult.Compatible;

        }

        private static MeshCompatibilityResult MaterialCompatibility(Material material) {

            if (material == null) {
                return MeshCompatibilityResult.MissingMaterial;
            }

            if (material.shader == null) {
                return MeshCompatibilityResult.MissingShader;
            }

            if (material.shader.name != "ParticleUI/Alpha") {
                return MeshCompatibilityResult.UnsupportedShader;
            }

            if (material.mainTextureScale != Vector2.one || material.mainTextureOffset != Vector2.zero) {
                return MeshCompatibilityResult.MaterialUvTransform;
            }

            return MeshCompatibilityResult.Compatible;

        }

        internal static string DescribeCompatibility(MeshCompatibilityResult result) {

            return result switch {
                MeshCompatibilityResult.Compatible => "Mesh compatible",
                MeshCompatibilityResult.MissingPrefab => "No prefab",
                MeshCompatibilityResult.NoParticleSystem => "Prefab has no ParticleSystem",
                MeshCompatibilityResult.UnsupportedSimulationSpace => "World/custom simulation space",
                MeshCompatibilityResult.TrailsEnabled => "Trails material and geometry",
                MeshCompatibilityResult.LightsEnabled => "Particle lights",
                MeshCompatibilityResult.TextureSheetAnimationEnabled => "Invalid sprite sheet: use Full Rect sprites sharing one texture",
                MeshCompatibilityResult.CustomDataEnabled => "Custom vertex data",
                MeshCompatibilityResult.MissingRenderer => "Missing particle renderer",
                MeshCompatibilityResult.UnsupportedRenderMode => "Non-billboard renderer",
                MeshCompatibilityResult.MissingMaterial => "Missing particle material",
                MeshCompatibilityResult.MissingShader => "Missing particle shader",
                MeshCompatibilityResult.UnsupportedShader => "Material is not certified for UI mesh rendering",
                MeshCompatibilityResult.MaterialUvTransform => "Material UV transform",
                _ => throw new System.ArgumentOutOfRangeException(nameof(result), result, null),
            };

        }


    }

}
