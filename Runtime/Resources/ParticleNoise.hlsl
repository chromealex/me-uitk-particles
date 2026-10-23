#ifndef PARTICLE_NOISE_INCLUDED
#define PARTICLE_NOISE_INCLUDED
uint _NoiseSeed;
// Analytic derivatives of a gradient-noise potential. Neighbouring particles
// sample the same field; particle random values only select strength curves.
uint NoiseHash(uint value) {
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    return value ^ (value >> 16);
}
float3 NoiseGradient(float3 position, uint seed) {
    int3 cell = (int3)floor(position);
    float3 t = frac(position);
    float3 fade = t * t * t * (t * (t * 6 - 15) + 10);
    float3 derivative = 30 * t * t * (t * (t - 2) + 1);
    float3 result = 0;
    [loop] for (uint corner = 0; corner < 8; corner++) {
        uint3 side = uint3(corner & 1u, (corner >> 1) & 1u, (corner >> 2) & 1u);
        uint3 lattice = (uint3)(cell + (int3)side);
        uint hash = NoiseHash(lattice.x ^ NoiseHash(lattice.y ^ NoiseHash(lattice.z ^ seed ^ _NoiseSeed)));
        // Twelve edge directions, unit length, with no floating-point hash.
        uint axis = hash % 3u;
        float a = (hash & 8u) != 0u ? .70710678 : -.70710678;
        float b = (hash & 16u) != 0u ? .70710678 : -.70710678;
        float3 gradient = axis == 0u ? float3(0, a, b) : (axis == 1u ? float3(a, 0, b) : float3(a, b, 0));
        float3 weight = lerp(1 - fade, fade, (float3)side);
        float3 dw = derivative * ((float3)side * 2 - 1);
        float value = dot(gradient, t - (float3)side);
        result += gradient * (weight.x * weight.y * weight.z)
            + value * dw * float3(weight.y * weight.z, weight.x * weight.z, weight.x * weight.y);
    }
    return result;
}
float3 CurlNoise(float3 position) {
    float3 c = NoiseGradient(position, 0x68bc21ebu);
    float3 result = float3(c.y, -c.x, 0);
    if (_NoiseSettings.w >= .5) {
        float3 a = NoiseGradient(position, 0x02e5be93u);
        float3 b = float3(0, 0, 0);
        if (_NoiseSettings.w > 1.5) {
            b = NoiseGradient(position, 0x967a889bu);
        }
        result = float3(c.y - b.z, a.z - c.x, b.x - a.y);
    }
    return result;
}
float3 SpatialNoiseVelocity(float3 position, float time, float age, float random) {
    float3 result = float3(0, 0, 0);
    if (_MotionEnabled.w >= 1.5) {
        float frequency = _NoiseSettings.x;
        float amplitude = 1, normalization = 0;
        float3 field = float3(0, 0, 0);
        // Keep octave and lattice iterations as loops. Expanding both multiplies
        // live temporaries across the two midpoint samples in the Metal kernel.
        [loop] for (int octave = 0; octave < (int)_NoiseSettings.z; octave++) {
            float3 samplePosition = position * frequency + float3(19.19, 73.73 + time * _NoiseSettings.y, 41.41);
            field += CurlNoise(samplePosition) * amplitude;
            normalization += amplitude;
            amplitude *= _OctaveSettings.x;
            frequency *= _OctaveSettings.y;
        }
        field *= 2 / max(normalization, .000001);
        // Bound the velocity so capture bounds remain finite without GPU readback.
        field *= min(1, 4 / max(length(field), .000001));
        result = field * MotionRange(age, 9, random) * _NoiseSettings.x;
    }
    return result;
}
#endif
