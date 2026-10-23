using System;

namespace ME.UITK.Particles {

    /// <summary>Immutable, in-memory checkpoint for one ParticleElement backend instance. Not a serialized asset.</summary>
    public sealed class ParticleState {

        internal readonly ParticleElement owner;
        internal readonly IParticleBackend backend;
        internal readonly Action validate, apply;
        public uint particleCount { get; }

        internal ParticleState(ParticleElement owner, IParticleBackend backend, uint count, Action validate, Action apply) {

            this.owner = owner;
            this.backend = backend;
            this.particleCount = count;
            this.validate = validate;
            this.apply = apply;

        }

    }

}
