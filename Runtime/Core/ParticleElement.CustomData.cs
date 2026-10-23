using System;
using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    public partial class ParticleElement {

        /// <summary>
        /// Reads live Custom1/Custom2 values. GPU evaluates the configured module at each particle's age;
        /// stateful GPU dynamics require synchronous readback. This does not bind arbitrary material vertex streams.
        /// </summary>
        public int GetCustomParticleData(List<Vector4> data, ParticleSystemCustomData stream, int systemIndex = 0) {

            if (data == null) {
                throw new ArgumentNullException(nameof(data));
            }

            if (stream != ParticleSystemCustomData.Custom1 && stream != ParticleSystemCustomData.Custom2) {
                throw new ArgumentOutOfRangeException(nameof(stream));
            }

            if (this.backend is GpuParticleBackend shader) {
                this.ValidateGpuSystem(systemIndex);
                return shader.GetSystem(systemIndex).GetCustomParticleData(data, stream);
            }

            return this.GetNativeSystem(systemIndex).GetCustomParticleData(data, stream);

        }

    }

}
