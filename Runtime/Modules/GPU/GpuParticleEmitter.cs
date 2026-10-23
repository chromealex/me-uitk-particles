using UnityEngine;

namespace ME.UITK.Particles {

    internal static class GpuParticleEmitter {

        internal static string Check(ParticleSystem ps) {

            if ((ps.inheritVelocity.enabled == true || ps.lifetimeByEmitterSpeed.enabled == true)
                && ps.main.emitterVelocityMode != ParticleSystemEmitterVelocityMode.Custom) {
                return "GPU Inherit Velocity and Lifetime by Emitter Speed require Main Emitter Velocity Mode = Custom (snapshot emitter velocity)";
            }

            return null;

        }

        internal static void Shape(ParticleSystem.ShapeModule shape, System.Random random, out Vector3 position, out Vector3 direction) {

            position = Vector3.zero;
            direction = Vector3.forward;
            if (shape.enabled == false) {
                return;
            }
            if (shape.shapeType == ParticleSystemShapeType.Box) {
                var sample = new Vector3((float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f, (float)random.NextDouble() - .5f);
                var rotation = Quaternion.Euler(shape.rotation);
                position = shape.position + rotation * Vector3.Scale(sample, shape.scale);
                direction = rotation * Vector3.forward;
                return;
            }
            var angle = (float)random.NextDouble() * Mathf.PI * 2f;
            direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            var innerRadius = 1f - shape.radiusThickness;
            position = direction * shape.radius * Mathf.Sqrt(Mathf.Lerp(innerRadius * innerRadius, 1f, (float)random.NextDouble()));

        }

        internal static float Lifetime(ParticleSystem ps, float lifetime, float random) {

            var module = ps.lifetimeByEmitterSpeed;
            if (module.enabled == true) {
                var range = module.range;
                var speed = ps.main.emitterVelocity.magnitude;
                var parameter = Mathf.Clamp01((speed - range.x) / Mathf.Max(.000001f, range.y - range.x));
                lifetime *= Mathf.Max(0f, module.curve.Evaluate(parameter, random));
            }

            return Mathf.Max(.000001f, lifetime);

        }

        internal static Vector3 Size(ParticleSystem.MainModule main, float random) {

            return main.startSize3D == true
                ? new Vector3(main.startSizeX.Evaluate(0f, random), main.startSizeY.Evaluate(0f, random), main.startSizeZ.Evaluate(0f, random))
                : Vector3.one * main.startSize.Evaluate(0f, random);

        }

        internal static float Width(ParticleSystem.Particle particle) {

            var size = particle.startSize3D;
            return Mathf.Max(Mathf.Abs(size.x), Mathf.Abs(size.y));

        }

        internal static Vector4 SizeRotation(ParticleSystem.Particle particle) {

            var size = particle.startSize3D;
            return new Vector4(size.x, particle.rotation * Mathf.Deg2Rad, size.y, size.z);

        }

        internal static Vector3 InitialVelocity(ParticleSystem ps, float random) {

            var module = ps.inheritVelocity;
            return module.enabled == true && module.mode == ParticleSystemInheritVelocityMode.Initial
                ? ps.transform.InverseTransformVector(ps.main.emitterVelocity) * module.curve.Evaluate(0f, random) : Vector3.zero;

        }

    }

}
