using System;

namespace ME.UITK.Particles {

    [Flags]
    public enum ParticleCapabilities {
        None = 0,
        Pause = 1 << 0,
        StopEmitting = 1 << 1,
        SimulationSpeed = 1 << 2,
        Position = 1 << 3,
        Tint = 1 << 4,
        Scale = 1 << 5,
        ParticleAccess = 1 << 6,
        Emit = 1 << 7,
        EmitIn = 1 << 8,
        CoordinateConversion = 1 << 9,
        EmitAt = 1 << 10,
        EmitAlongBorder = 1 << 11,
        Attractor = 1 << 12,
        ManualSimulation = 1 << 13,
        RandomSeed = 1 << 14,
        AsyncPlayback = 1 << 15,
        Prewarm = 1 << 16,
        EmitParameters = 1 << 17,
        EmitTowards = 1 << 18,
        ArrivalEvents = 1 << 19,
        EmissionRate = 1 << 20,
        EmissionArea = 1 << 21,
        StateCapture = 1 << 22,
    }

}
