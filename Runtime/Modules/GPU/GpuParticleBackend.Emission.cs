using UnityEngine;

namespace ME.UITK.Particles {

    internal sealed partial class GpuParticleBackend {

        private ParticleSystem.Burst[] bursts;
        private int nextBurst;
        private bool automaticBursts;
        private bool hasPendingBursts => this.automaticBursts == true && this.nextBurst < this.bursts.Length;

        private int CaptureBursts(ParticleSystem.EmissionModule emission) {

            this.bursts = new ParticleSystem.Burst[emission.burstCount];
            emission.GetBursts(this.bursts);
            System.Array.Sort(this.bursts, (a, b) => a.time.CompareTo(b.time));
            var count = 0u;
            foreach (var burst in this.bursts) {
                count += (uint)Mathf.RoundToInt(burst.count.constant);
            }
            // Compatibility bounds the whole schedule to min(maxParticles, 16000).
            return (int)count;

        }

        private void AdvanceSimulation(float targetTime) {

            // Stop at each birth time, even when a large manual step spans several
            // waves. Existing particles keep their age; new particles start at zero.
            while (this.hasPendingBursts == true && this.bursts[this.nextBurst].time <= targetTime) {
                var burst = this.bursts[this.nextBurst++];
                this.elapsed = burst.time;
                this.dynamics?.Step(this.elapsed);
                this.AdvanceChildren();
                var count = Mathf.Min(Mathf.RoundToInt(burst.count.constant), this.availableEmissionCount);
                this.Emit(count, null);
            }
            this.elapsed = targetTime;
            this.dynamics?.Step(this.elapsed);
            this.AdvanceChildren();

        }

    }
}
