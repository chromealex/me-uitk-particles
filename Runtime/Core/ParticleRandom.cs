namespace ME.UITK.Particles {

    // Explicit state makes placement reproducible after a checkpoint without reflection into System.Random.
    internal sealed class ParticleRandom {

        internal uint state;

        internal ParticleRandom(int seed) {

            this.state = unchecked((uint)seed);
            if (this.state == 0) {
                this.state = 0x9e3779b9u;
            }

        }

        internal double NextDouble() {

            var value = this.state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            this.state = value;
            return value / 4294967296d;

        }

    }

}
