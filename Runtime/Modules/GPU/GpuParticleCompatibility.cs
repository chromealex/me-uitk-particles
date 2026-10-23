using UnityEngine;

namespace ME.UITK.Particles {

    internal static class GpuParticleCompatibility {

        internal static string Check(GameObject source) {

            if (source == null) {
                return "No prefab";
            }

            using var systemsLease = UnityEngine.Pool.ListPool<ParticleSystem>.Get(out var systems);
            source.GetComponentsInChildren(true, systems);
            var root = source.GetComponent<ParticleSystem>();
            if (root == null || source.activeSelf == false || systems.Count > 9) {
                return "GPU requires an active root ParticleSystem and at most 8 direct sub-emitter systems";
            }

            var subs = root.subEmitters;
            using var referencedLease = UnityEngine.Pool.HashSetPool<ParticleSystem>.Get(out var referenced);
            if (subs.enabled == true) {
                for (var i = 0; i < subs.subEmittersCount; i++) {
                    var child = subs.GetSubEmitterSystem(i);
                    var type = subs.GetSubEmitterType(i);
                    if (child == null || child.transform.parent != source.transform || child.gameObject.activeSelf == false
                        || child.transform.localPosition != Vector3.zero || child.transform.localRotation != Quaternion.identity
                        || child.subEmitters.enabled == true) {
                        return "GPU Sub Emitters require active direct children with zero local position/rotation and no nested Sub Emitters";
                    }

                    if (type != ParticleSystemSubEmitterType.Birth && type != ParticleSystemSubEmitterType.Death) {
                        return "GPU Sub Emitters support Birth and Death; unsupported event: " + type;
                    }

                    var supported = ParticleSystemSubEmitterProperties.InheritColor | ParticleSystemSubEmitterProperties.InheritSize | ParticleSystemSubEmitterProperties.InheritRotation;
                    if ((subs.GetSubEmitterProperties(i) & ~supported) != 0) {
                        return "GPU Sub Emitters support Inherit Color, Size and Rotation; Lifetime and Duration inheritance are unsupported";
                    }

                    if (child.main.simulationSpeed != root.main.simulationSpeed || child.main.useUnscaledTime != root.main.useUnscaledTime) {
                        return "GPU Sub Emitters require the same simulationSpeed and useUnscaledTime as the root";
                    }

                    referenced.Add(child);
                }
            }

            foreach (var system in systems) {
                if (system != root && referenced.Contains(system) == false) {
                    return "GPU hierarchy contains a system not registered as a root Sub Emitter: " + system.name;
                }

                if (system != root && (system.emission.burstCount != 1 || system.emission.GetBurst(0).time != 0f)) {
                    return "GPU Sub Emitters require one burst at time zero; scheduled waves are supported on the root";
                }

                var reason = GpuParticleCompatibility.CheckSystem(system);
                if (reason != null) {
                    return system.name + ": " + reason;
                }
            }

            using var renderersLease = UnityEngine.Pool.ListPool<Renderer>.Get(out var renderers);
            source.GetComponentsInChildren(true, renderers);
            if (renderers.Count != systems.Count || source.GetComponentInChildren<UnityEngine.VFX.VisualEffect>(true) != null
                || source.GetComponentInChildren<MonoBehaviour>(true) != null) {
                return "GPU does not execute prefab scripts or additional renderers";
            }

            return null;

        }

        private static string CheckSystem(ParticleSystem ps) {

            var source = ps.gameObject;
            var main = ps.main;
            if (source.transform.localScale != Vector3.one || main.simulationSpace != ParticleSystemSimulationSpace.Local
                || main.ringBufferMode != ParticleSystemRingBufferMode.Disabled || main.gravitySource != ParticleSystemGravitySource.Physics3D
                || main.loop == true || main.prewarm == true || main.startRotation3D == true
                || main.flipRotation != 0 || main.startDelay.mode != ParticleSystemCurveMode.Constant || main.startDelay.constant != 0
                || main.simulationSpeed <= 0 || float.IsFinite(main.simulationSpeed) == false) {
                return "GPU requires a local, non-looping burst with uniform scale and Z-only start rotation";
            }

            if (GpuParticleCompatibility.IsRange(main.startLifetime) == false || main.startLifetime.Evaluate(0, 0) <= 0
                || GpuParticleCompatibility.IsRange(main.startSpeed) == false || (main.startSize3D == true
                    ? IsRange(main.startSizeX) == false || IsRange(main.startSizeY) == false || IsRange(main.startSizeZ) == false
                    : IsRange(main.startSize) == false)
                || GpuParticleCompatibility.IsRange(main.startRotation) == false || main.gravityModifier.mode != ParticleSystemCurveMode.Constant
                || (main.startColor.mode != ParticleSystemGradientMode.Color && main.startColor.mode != ParticleSystemGradientMode.TwoColors)) {
                return "GPU supports constant or two-constant start values, constant gravity and one/two start colors";
            }

            var emission = ps.emission;
            if (emission.enabled == false || emission.burstCount < 1 || emission.rateOverTime.mode != ParticleSystemCurveMode.Constant
                || emission.rateOverTime.constant != 0 || emission.rateOverDistance.mode != ParticleSystemCurveMode.Constant || emission.rateOverDistance.constant != 0) {
                return "GPU requires at least one burst and no continuous emission";
            }

            var total = 0u;
            for (var i = 0; i < emission.burstCount; i++) {
                var burst = emission.GetBurst(i);
                if (float.IsFinite(burst.time) == false || burst.time < 0f || burst.time >= main.duration
                    || burst.cycleCount != 1 || burst.probability != 1 || burst.count.mode != ParticleSystemCurveMode.Constant
                    || float.IsFinite(burst.count.constant) == false || burst.count.constant < 1 || burst.count.constant > 16000
                    || burst.count.constant != Mathf.Floor(burst.count.constant)) {
                    return "GPU bursts require times within Duration, one cycle, probability 1 and constant whole counts from 1 to 16000";
                }
                total += (uint)burst.count.constant;
                if (total > (uint)main.maxParticles || total > 16000u) {
                    return "GPU burst schedule must total at most min(maxParticles, 16000) particles";
                }
            }

            var shape = ps.shape;
            if (shape.enabled == true) {
                if (shape.alignToDirection == true || shape.randomDirectionAmount != 0 || shape.sphericalDirectionAmount != 0
                    || shape.randomPositionAmount != 0 || shape.texture != null) {
                    return "GPU Shape requires no direction randomization, Align to Direction, position spread or texture";
                }
                if (shape.shapeType != ParticleSystemShapeType.Box && (shape.shapeType != ParticleSystemShapeType.Circle || shape.arc != 360
                    || shape.arcMode != ParticleSystemShapeMultiModeValue.Random || shape.radiusMode != ParticleSystemShapeMultiModeValue.Random
                    || shape.position != Vector3.zero || shape.rotation != Vector3.zero || shape.scale != Vector3.one)) {
                    return "GPU Shape supports Box volume with transforms, an untransformed random full Circle, or disabled Shape";
                }
            }

            var rotation = ps.rotationOverLifetime;
            if (rotation.enabled == true && rotation.separateAxes == true) {
                return "GPU Rotation over Lifetime supports Z rotation only (disable Separate Axes)";
            }

            var velocity = ps.velocityOverLifetime;
            if (velocity.enabled == true && (velocity.space != ParticleSystemSimulationSpace.Local
                || IsConstant(velocity.orbitalX, 0f) == false || IsConstant(velocity.orbitalY, 0f) == false || IsConstant(velocity.orbitalZ, 0f) == false
                || IsConstant(velocity.radial, 0f) == false || IsConstant(velocity.speedModifier, 1f) == false)) {
                return "GPU Velocity over Lifetime requires Local space, zero Orbital/Radial velocity and Speed Modifier = 1";
            }

            var force = ps.forceOverLifetime;
            if (force.enabled == true && (force.space != ParticleSystemSimulationSpace.Local || force.randomized == true)) {
                return "GPU Force over Lifetime requires Local space and Randomize disabled";
            }

            var noise = ps.noise;
            if (noise.enabled == true && (noise.remapEnabled == true || IsConstant(noise.rotationAmount, 0f) == false
                || IsConstant(noise.sizeAmount, 0f) == false || noise.positionAmount.mode != ParticleSystemCurveMode.Constant
                || noise.scrollSpeed.mode != ParticleSystemCurveMode.Constant || float.IsFinite(noise.frequency) == false || noise.frequency < 0f
                || float.IsFinite(noise.scrollSpeed.constant) == false || float.IsFinite(noise.positionAmount.constant) == false
                || float.IsFinite(noise.octaveMultiplier) == false || noise.octaveMultiplier < 0f
                || float.IsFinite(noise.octaveScale) == false || noise.octaveScale < 0f || noise.octaveCount < 1 || noise.octaveCount > 4)) {
                return "GPU Noise supports position turbulence: constant Position Amount/Scroll Speed, no Remap, Rotation Amount = 0, Size Amount = 0, 1-4 octaves";
            }

            if (ps.sizeOverLifetime.enabled == true && (ps.sizeOverLifetime.separateAxes == true
                || (ps.sizeOverLifetime.size.mode != ParticleSystemCurveMode.Constant && ps.sizeOverLifetime.size.mode != ParticleSystemCurveMode.Curve))) {
                return "GPU supports a single size-over-lifetime curve";
            }

            if (ps.colorOverLifetime.enabled == true && ps.colorOverLifetime.color.mode != ParticleSystemGradientMode.Color
                && ps.colorOverLifetime.color.mode != ParticleSystemGradientMode.Gradient) {
                return "GPU supports a single color-over-lifetime gradient";
            }

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer == null || renderer.enabled == false || renderer.renderMode != ParticleSystemRenderMode.Billboard
                || renderer.alignment != ParticleSystemRenderSpace.View || renderer.pivot != Vector3.zero || renderer.flip != Vector3.zero
                || renderer.sortMode != ParticleSystemSortMode.None || renderer.minParticleSize != 0) {
                return "GPU requires unsorted view-aligned billboards with default pivot";
            }

            var emitterReason = GpuParticleEmitter.Check(ps);
            if (emitterReason != null) {
                return emitterReason;
            }

            var dynamicsReason = GpuParticleDynamics.Check(ps);
            if (dynamicsReason != null) {
                return dynamicsReason;
            }

            var lightReason = GpuParticleLights.Check(ps);
            if (lightReason != null) {
                return lightReason;
            }

            var appearanceReason = GpuParticleAppearance.Check(ps);
            if (appearanceReason != null) {
                return appearanceReason;
            }

            var trailReason = GpuParticleTrails.Check(ps);
            if (trailReason != null) {
                return trailReason;
            }

            var material = renderer.sharedMaterial;
            if (material == null || material.shader == null || material.shader.name != "ParticleUI/Alpha"
                || material.mainTextureScale != Vector2.one || material.mainTextureOffset != Vector2.zero) {
                return "GPU requires the ParticleUI/Alpha material without UV transforms";
            }

            var shader = Resources.Load<UnityEngine.Shader>("ProceduralParticles");
            if (shader == null || shader.isSupported == false || SystemInfo.graphicsShaderLevel < 30 || SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat) == false) {
                return "Procedural particle shader is unavailable on this device";
            }

            return null;

        }

        private static bool IsConstant(ParticleSystem.MinMaxCurve curve, float value) {

            return curve.mode == ParticleSystemCurveMode.Constant && curve.constant == value;

        }

        private static bool IsRange(ParticleSystem.MinMaxCurve curve) {

            return (curve.mode == ParticleSystemCurveMode.Constant || curve.mode == ParticleSystemCurveMode.TwoConstants)
                && float.IsFinite(curve.constantMin) == true && float.IsFinite(curve.constantMax) == true;

        }

    }

}
