using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    public sealed partial class ParticleElement {

        private sealed class ParticleFlight {

            internal VisualElement destination;
            internal Vector2 start;
            internal float duration;
            internal double elapsed, lastTime;
            internal int systemIndex;
            internal readonly HashSet<uint> seeds = new();

        }

        private readonly struct Arrival {

            internal readonly VisualElement destination;
            internal readonly uint count;
            internal readonly int systemIndex;
            internal readonly Vector2 position;

            internal Arrival(VisualElement destination, uint count, int systemIndex, Vector2 position) {

                this.destination = destination;
                this.count = count;
                this.systemIndex = systemIndex;
                this.position = position;

            }

        }

        private readonly List<ParticleFlight> flights = new();
        private readonly List<Arrival> arrivals = new();
        private readonly List<uint> flightSeeds = new();
        private readonly HashSet<uint> occupiedSeeds = new();
        private readonly HashSet<uint> survivingSeeds = new();
        private ParticleSystem.Particle[] flightBuffer = Array.Empty<ParticleSystem.Particle>();
        private uint nextFlightSeed = uint.MaxValue, flightVersion;
        private bool updatingFlights;

        /// <summary>Emits a burst with optional overrides in the selected system's simulation space.</summary>
        public void Emit(ParticleEmitParams parameters, uint count, int systemIndex = 0) {

            parameters.Validate();
            var accepted = this.ValidateEmission(count, systemIndex);
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            this.EmitParticles(accepted, systemIndex, null, parameters);

        }

        /// <summary>
        /// Emits from the source center and moves live particles to the destination's current center in travelTime seconds.
        /// Pause and simulationSpeed affect the transfer clock. Arrival removes particles and sends ParticleArrivedEvent.
        /// Stop/restart/detach cancel transfers. External removal or seed edits cancel tracking for those particles.
        /// </summary>
        public void EmitTowards(VisualElement from, VisualElement to, uint count, float travelTime, int systemIndex = 0) {

            if (float.IsFinite(travelTime) == false || travelTime <= 0f) {
                throw new ArgumentOutOfRangeException(nameof(travelTime));
            }

            var accepted = this.ValidateEmission(count, systemIndex);
            var sourceSize = this.ValidateTarget(from, systemIndex);
            this.ValidateTarget(to, systemIndex);
            var speed = this.GetFlightPrefabSpeed(systemIndex);
            var lifetime = travelTime * Mathf.Max(1f, speed) + 1f;
            if (float.IsFinite(lifetime) == false) {
                throw new ArgumentOutOfRangeException(nameof(travelTime));
            }
            if (accepted == 0) {
                return;
            }

            this.PrepareManualEmission();
            var start = from.ChangeCoordinatesTo(this, sourceSize * .5f);
            var flight = new ParticleFlight {
                destination = to,
                start = start,
                duration = travelTime,
                systemIndex = systemIndex,
                lastTime = ParticleEnvironment.Now(false),
            };
            var liveCount = this.ReadFlightParticles(systemIndex);
            this.occupiedSeeds.Clear();
            for (var i = 0; i < liveCount; i++) {
                this.occupiedSeeds.Add(this.flightBuffer[i].randomSeed);
            }

            this.flightSeeds.Clear();
            for (var i = 0; i < accepted; i++) {
                while (this.nextFlightSeed == 0 || this.occupiedSeeds.Contains(this.nextFlightSeed) == true) {
                    this.nextFlightSeed = unchecked(this.nextFlightSeed - 1u);
                }
                var seed = this.nextFlightSeed;
                this.nextFlightSeed = unchecked(this.nextFlightSeed - 1u);
                this.flightSeeds.Add(seed);
                flight.seeds.Add(seed);
                this.occupiedSeeds.Add(seed);
            }

            this.EmitParticles(accepted, systemIndex, null, new ParticleEmitParams {
                position = this.LocalToSimulation(start, systemIndex),
                velocity = Vector3.zero,
                startLifetime = lifetime,
            }, this.flightSeeds);
            this.flights.Add(flight);

        }

        private int ReadFlightParticles(int systemIndex) {

            var count = checked((int)this.GetParticleCount(systemIndex));
            if (this.flightBuffer.Length < count) {
                Array.Resize(ref this.flightBuffer, count);
            }

            return this.GetParticles(this.flightBuffer, systemIndex: systemIndex);

        }

        private float GetFlightPrefabSpeed(int systemIndex) {

            return this.backend is NativeParticleBackend native ? native.GetPrefabSpeed(systemIndex)
                : ((GpuParticleBackend)this.backend).GetSystem(systemIndex).prefabSimulationSpeed;

        }

        // A caller may manually step far beyond the flight duration in one call. Keep tracked particles
        // alive through that simulation step so the arrival pass can place and consume them exactly once.
        private void ProtectFlightsForStep(float deltaTime) {

            foreach (var flight in this.flights) {
                var lifetime = (double)deltaTime * this.GetFlightPrefabSpeed(flight.systemIndex) * this.simulationSpeed + 1d;
                var count = this.ReadFlightParticles(flight.systemIndex);
                for (var i = 0; i < count; i++) {
                    if (flight.seeds.Contains(this.flightBuffer[i].randomSeed) == true) {
                        this.flightBuffer[i].remainingLifetime = Mathf.Max(this.flightBuffer[i].remainingLifetime, (float)Math.Min(lifetime, float.MaxValue));
                        this.flightBuffer[i].startLifetime = Mathf.Max(this.flightBuffer[i].startLifetime, this.flightBuffer[i].remainingLifetime);
                    }
                }
                this.SetParticles(this.flightBuffer, count, systemIndex: flight.systemIndex);
            }

        }

        private void UpdateFlights(float deltaTime, bool manual) {

            if (this.updatingFlights == true || this.flights.Count == 0 || this.backend == null || this.requestedPlay == false) {
                return;
            }

            this.updatingFlights = true;
            var version = this.flightVersion;
            try {
                this.arrivals.Clear();
                var now = ParticleEnvironment.Now(false);
                for (var flightIndex = this.flights.Count - 1; flightIndex >= 0; flightIndex--) {
                    var flight = this.flights[flightIndex];
                    var step = manual == true ? deltaTime : Math.Max(0d, now - flight.lastTime);
                    flight.lastTime = now;
                    flight.elapsed += step * this.simulationSpeed;
                    var target = flight.destination;
                    var valid = this.panel != null && target.panel == this.panel && target.layout.width > 0f && target.layout.height > 0f;
                    var destination = valid == true ? target.ChangeCoordinatesTo(this, target.layout.size * .5f) : default;
                    valid &= float.IsFinite(destination.x) == true && float.IsFinite(destination.y) == true;
                    var arrived = flight.elapsed >= flight.duration;
                    var count = this.ReadFlightParticles(flight.systemIndex);
                    var arrivedCount = 0u;
                    this.survivingSeeds.Clear();
                    var position = valid == true ? this.LocalToSimulation(Vector2.Lerp(flight.start, destination, (float)(flight.elapsed / flight.duration)), flight.systemIndex) : default;
                    for (var i = 0; i < count; i++) {
                        if (flight.seeds.Contains(this.flightBuffer[i].randomSeed) == false) {
                            continue;
                        }

                        this.survivingSeeds.Add(this.flightBuffer[i].randomSeed);
                        this.flightBuffer[i].position = position;
                        this.flightBuffer[i].velocity = Vector3.zero;
                        if (arrived == true || valid == false) {
                            this.flightBuffer[i].remainingLifetime = -1f;
                            if (valid == true) {
                                arrivedCount++;
                            }
                        }
                    }

                    flight.seeds.IntersectWith(this.survivingSeeds);
                    this.SetParticles(this.flightBuffer, count, systemIndex: flight.systemIndex);
                    if (arrived == true || valid == false || flight.seeds.Count == 0) {
                        this.flights.RemoveAt(flightIndex);
                    }
                    if (arrivedCount > 0) {
                        this.arrivals.Add(new Arrival(target, arrivedCount, flight.systemIndex, destination));
                    }
                }

                // Mutations are finished before invoking user callbacks. A restart/dispose invalidates old notifications.
                for (var i = 0; i < this.arrivals.Count && version == this.flightVersion; i++) {
                    var arrival = this.arrivals[i];
                    this.SendArrival(arrival.destination, arrival.count, arrival.systemIndex, arrival.position);
                }
            } finally {
                this.arrivals.Clear();
                this.updatingFlights = false;
            }

        }

        private void SendArrival(VisualElement destination, uint count, int systemIndex, Vector2 position) {

            if (count == 0) {
                return;
            }

            using (var evt = ParticleArrivedEvent.GetPooled()) {
                evt.target = this;
                evt.destination = destination;
                evt.arrivedCount = count;
                evt.systemIndex = systemIndex;
                evt.localPosition = position;
                this.SendEvent(evt);
            }

        }

        private void ResetFlightClocks() {

            var now = ParticleEnvironment.Now(false);
            foreach (var flight in this.flights) {
                flight.lastTime = now;
            }

        }

        private void ClearFlights() {

            this.flights.Clear();
            this.flightVersion = unchecked(this.flightVersion + 1u);

        }

    }

}
