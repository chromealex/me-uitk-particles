// <summary>
// Samples native particle simulation to estimate effect bounds before RenderTexture playback.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    /// <summary>Samples native simulation before playback; live bounds cover later/random changes.</summary>
    internal static class ParticleBoundsCalculator {

        private const float MaximumPreviewSeconds = 15f;
        private const int MaximumPreviewSteps = 450;

        public static Bounds Calculate(ParticleSystem[] systems, ParticleSystemRenderer[] renderers, Vector3 origin) {

            using var rootsLease = UnityEngine.Pool.ListPool<ParticleSystem>.Get(out var roots);
            using var seedsLease = UnityEngine.Pool.ListPool<uint>.Get(out var seeds);
            using var automaticSeedsLease = UnityEngine.Pool.ListPool<bool>.Get(out var automaticSeeds);
            var duration = 0f;
            for (var i = 0; i < systems.Length; i++) {
                var system = systems[i];
                seeds.Add(system.randomSeed);
                automaticSeeds.Add(system.useAutoRandomSeed);
                var main = system.main;
                // Summing durations conservatively includes delayed sub-emitter chains.
                duration += main.duration + ParticleBoundsCalculator.Maximum(main.startDelay) + ParticleBoundsCalculator.Maximum(main.startLifetime);
                if (system.trails.enabled == true) {
                    duration += ParticleBoundsCalculator.Maximum(system.trails.lifetime);
                }

                var parent = system.transform.parent;
                var nested = false;
                while (parent != null) {
                    if (parent.TryGetComponent<ParticleSystem>(out _) == true) {
                        nested = true;
                        break;
                    }

                    parent = parent.parent;
                }

                if (nested == false) {
                    roots.Add(system);
                }
            }

            var bounds = new Bounds(Vector3.zero, Vector3.one * .1f);
            var seconds = Mathf.Clamp(duration, 1f, ParticleBoundsCalculator.MaximumPreviewSeconds);
            var steps = Mathf.Min(Mathf.CeilToInt(seconds * 30), ParticleBoundsCalculator.MaximumPreviewSteps);
            try {
                for (var step = 0; step < steps; step++) {
                    foreach (var root in roots) {
                        root.Simulate(seconds / steps, true, step == 0, false);
                    }

                    ParticleBoundsCalculator.Encapsulate(ref bounds, renderers, origin);
                }
            } finally {
                foreach (var root in roots) {
                    root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    root.Simulate(0, true, true, false);
                    root.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }

                for (var i = 0; i < systems.Length; i++) {
                    systems[i].randomSeed = seeds[i];
                    systems[i].useAutoRandomSeed = automaticSeeds[i];
                }
            }

            // Sampling is an estimate, not a mathematical bound for arbitrary modules or shaders.
            bounds.Expand(Vector3.Max(bounds.size * .1f, Vector3.one * .2f));
            return bounds;

        }

        public static void Encapsulate(ref Bounds bounds, ParticleSystemRenderer[] renderers, Vector3 origin) {

            foreach (var renderer in renderers) {
                if (renderer == null || renderer.gameObject.activeInHierarchy == false) {
                    continue;
                }

                // Native world-space renderer bounds also cover mesh particles and trails.
                var current = renderer.bounds;
                current.center -= origin;
                if (ParticleBoundsCalculator.IsFinite(current.center) == false || ParticleBoundsCalculator.IsFinite(current.size) == false || current.size == Vector3.zero) {
                    continue;
                }

                bounds.Encapsulate(current);
            }

        }

        private static bool IsFinite(Vector3 value) {

            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        }

        private static float Maximum(ParticleSystem.MinMaxCurve curve) {

            if (curve.mode == ParticleSystemCurveMode.Constant) {
                return curve.constant;
            }

            if (curve.mode == ParticleSystemCurveMode.TwoConstants) {
                return Mathf.Max(curve.constantMin, curve.constantMax);
            }

            var maximum = 0f;
            for (var i = 0; i <= 32; i++) {
                var time = i / 32f;
                maximum = Mathf.Max(maximum, Mathf.Max(curve.Evaluate(time, 0), curve.Evaluate(time, 1)));
            }

            return maximum;

        }

    }

}
