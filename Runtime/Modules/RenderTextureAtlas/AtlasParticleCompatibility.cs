using UnityEngine;

namespace ME.UITK.Particles {

    internal static class AtlasParticleCompatibility {

        internal static string Check(GameObject prefab) {

            if (prefab == null) {
                return "Missing prefab";
            }

            using var systemsLease = UnityEngine.Pool.ListPool<ParticleSystem>.Get(out var systems);
            prefab.GetComponentsInChildren(true, systems);
            if (systems.Count == 0) {
                return "No ParticleSystem";
            }

            using var materialsLease = UnityEngine.Pool.ListPool<Material>.Get(out var materials);
            foreach (var system in systems) {
                if (system.main.simulationSpace != ParticleSystemSimulationSpace.Local
                    || system.main.scalingMode == ParticleSystemScalingMode.Shape
                    || system.velocityOverLifetime.enabled == true && system.velocityOverLifetime.space != ParticleSystemSimulationSpace.Local
                    || system.forceOverLifetime.enabled == true && system.forceOverLifetime.space != ParticleSystemSimulationSpace.Local) {
                    return "Requires local-space simulation";
                }

                if (system.trails.enabled == true && system.trails.worldSpace == true) {
                    return "World-space trails";
                }

                if (system.collision.enabled == true || system.trigger.enabled == true || system.externalForces.enabled == true
                    || system.lights.enabled == true || system.inheritVelocity.enabled == true || system.main.gravityModifierMultiplier != 0f) {
                    return "World interactions are not atlas compatible";
                }

                var shape = system.shape;
                if (shape.enabled == true && (shape.meshRenderer != null || shape.skinnedMeshRenderer != null || shape.spriteRenderer != null)) {
                    return "External shape renderer";
                }

                var renderer = system.GetComponent<ParticleSystemRenderer>();
                if (renderer == null || renderer.renderMode != ParticleSystemRenderMode.Billboard || renderer.alignment != ParticleSystemRenderSpace.View
                    || renderer.sortMode != ParticleSystemSortMode.None) {
                    return "Requires view-aligned billboards";
                }

                renderer.GetSharedMaterials(materials);
                foreach (var material in materials) {
                    if (material == null || material.shader == null || material.shader.name != "ParticleUI/Alpha") {
                        return "Material is not certified for atlas rendering";
                    }
                }
            }

            // Relocation must not change physics, animation or other component behaviour.
            foreach (var component in prefab.GetComponentsInChildren<Component>(true)) {
                if ((component is Transform) == false && (component is ParticleSystem) == false && (component is ParticleSystemRenderer) == false) {
                    return "Additional components are not atlas compatible";
                }
            }

            return null;

        }

    }

}
