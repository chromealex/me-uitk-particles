#ifndef PARTICLE_MOTION_INCLUDED
#define PARTICLE_MOTION_INCLUDED
Texture2D<float4> _Motion;
SamplerState sampler_Motion;
float4 _MotionEnabled, _NoiseSettings, _OctaveSettings;
float4 MotionSample(float age, float row) {
    return _Motion.SampleLevel(sampler_Motion, float2((saturate(age) * 255 + .5) / 256, (row + .5) / 16), 0);
}
float3 MotionRange(float age, float row, float random) {
    return lerp(MotionSample(age, row).xyz, MotionSample(age, row + 1).xyz, random);
}
void EvaluateMotion(float age, float lifetime, float random, out float3 displacement, out float3 velocity, out float angle, out float angularVelocity) {
    displacement = 0;
    velocity = 0;
    angle = 0;
    angularVelocity = 0;
    if (_MotionEnabled.x > .5) {
        float4 rotation = MotionSample(age, 0);
        angle = lifetime * lerp(rotation.b, rotation.a, random);
        angularVelocity = lerp(rotation.r, rotation.g, random);
    }
    if (_MotionEnabled.y > .5) {
        displacement += lifetime * MotionRange(age, 3, random);
        velocity += MotionRange(age, 1, random);
    }
    if (_MotionEnabled.z > .5) {
        displacement += lifetime * lifetime * MotionRange(age, 13, random);
        velocity += lifetime * MotionRange(age, 7, random);
    }
    if (_MotionEnabled.w > .5 && _MotionEnabled.w < 1.5) {
        float3 strength = MotionRange(age, 9, random);
        float3 derivative = MotionRange(age, 11, random) / max(lifetime, .000001);
        float3 wave = 0, slope = 0;
        float amplitude = 1, normalization = 0;
        // Match the CPU analytic sampler: cycles to radians, normalized displacement.
        float frequency = _NoiseSettings.x * (1 + _NoiseSettings.y) * 6.2831853;
        [unroll] for (int octave = 0; octave < 4; octave++) {
            if (octave < (int)_NoiseSettings.z) {
                float phase = random * 6.2831853 + octave * 1.618034;
                float3 rates = float3(1, 1.17 + _NoiseSettings.w * .11, 1.43 + _NoiseSettings.w * .13) * frequency;
                float3 t = rates * (age * lifetime) + float3(phase, phase + 2.1, phase + 4.2);
                wave += sin(t) * amplitude;
                slope += cos(t) * rates * amplitude;
                normalization += amplitude;
                amplitude *= _OctaveSettings.x;
                frequency *= _OctaveSettings.y;
            }
        }
        wave /= max(normalization, .000001) * 6.2831853;
        slope /= max(normalization, .000001) * 6.2831853;
        displacement += strength * wave;
        velocity += derivative * wave + strength * slope;
    }
}

#endif
