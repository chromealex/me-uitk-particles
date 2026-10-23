using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    internal sealed partial class GpuParticleBackend {

        internal int GetCustomParticleData(List<Vector4> data, ParticleSystemCustomData stream) {

            this.ReadDynamics();
            data.Clear();
            var module = this.sourceSystem.customData;
            var mode = module.GetMode(stream);
            if (module.enabled == false || mode == ParticleSystemCustomDataMode.Disabled) {
                return 0;
            }

            for (var i = 0; i < this.liveCount; i++) {
                var particle = this.EvaluateParticle(i);
                if (particle.remainingLifetime <= 0f) {
                    continue;
                }

                var age = Mathf.Clamp01(1f - particle.remainingLifetime / Mathf.Max(.000001f, particle.startLifetime));
                var random = GpuParticleMotion.Random(particle.randomSeed);
                var value = Vector4.zero;
                if (mode == ParticleSystemCustomDataMode.Color) {
                    value = module.GetColor(stream).Evaluate(age, random);
                } else {
                    for (var component = 0; component < module.GetVectorComponentCount(stream); component++) {
                        value[component] = module.GetVector(stream, component).Evaluate(age, random);
                    }
                }

                data.Add(value);
            }

            return data.Count;

        }

    }

}
