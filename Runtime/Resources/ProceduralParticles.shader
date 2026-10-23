Shader "Hidden/ParticleUI/ProceduralParticles" {
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _LightSettings ("Glow settings", Vector) = (0,0,0,0)
        [HideInInspector] _LightOptions ("Glow options", Vector) = (0,0,0,0)
        [HideInInspector] _LightColor ("Glow color", Color) = (1,1,1,1)
        [HideInInspector] _DynamicsTexture ("GPU state", 2D) = "black" {}
        [HideInInspector] _DynamicsSize ("GPU state dimensions", Vector) = (512,3,0,0)
        [HideInInspector] _DynamicsEnabled ("GPU dynamics enabled", Float) = 0
        [HideInInspector] _Appearance ("Appearance curves", 2D) = "white" {}
        [HideInInspector] _AppearanceEnabled ("Appearance enabled", Vector) = (0,0,0,0)
        [HideInInspector] _RotationSpeed ("Rotation by speed", Vector) = (0,1,0,0)
        [HideInInspector] _SpeedRanges ("Speed ranges", Vector) = (0,1,0,1)
        [HideInInspector] _SheetSpriteCount ("Sprite frames", Float) = 0
        [HideInInspector] _SheetSpriteUvs ("Sprite frame UVs", 2D) = "black" {}
        [HideInInspector] _SheetGrid ("Sheet grid", Vector) = (1,1,0,1)
        [HideInInspector] _SheetTime ("Sheet time", Vector) = (0,0,0,1)
        [HideInInspector] _SheetRow ("Sheet row", Vector) = (0,0,0,0)
        [HideInInspector] _Lifetime ("Lifetime curves", 2D) = "white" {}
        [HideInInspector] _Motion ("Motion curves", 2D) = "black" {}
        [HideInInspector] _TrailCurves ("Trail curves", 2D) = "white" {}
        [HideInInspector] _TrailSettings ("Trail settings", Vector) = (0,0,0,0)
        [HideInInspector] _TrailTextureScale ("Trail texture scale", Vector) = (1,1,0,0)
        [HideInInspector] _MotionEnabled ("Motion enabled", Vector) = (0,0,0,0)
        [HideInInspector] _NoiseSettings ("Noise settings", Vector) = (0,0,0,0)
        [HideInInspector] _OctaveSettings ("Octave settings", Vector) = (0,0,0,0)
        [HideInInspector] _Gravity ("Gravity", Vector) = (0,0,0,0)
        [HideInInspector] _StateTime ("State time", Float) = 0
        [HideInInspector] _Elapsed ("Elapsed", Float) = 0
        [HideInInspector] _MaxSize ("Maximum size", Float) = 1
        [HideInInspector] _ApplyColorSpace ("Apply color space", Float) = 0
    }
    SubShader {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
            ZWrite Off Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_local _ PARTICLE_TRAILS PARTICLE_GLOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_Lifetime); SAMPLER(sampler_Lifetime);
            TEXTURE2D(_TrailCurves); SAMPLER(sampler_TrailCurves);
            TEXTURE2D(_DynamicsTexture); SAMPLER(sampler_DynamicsTexture);
            float4 _DynamicsSize;
            float _DynamicsEnabled;
            TEXTURE2D(_Appearance); SAMPLER(sampler_Appearance);
            float4 _RotationSpeed;
            float4 _LightSettings, _LightOptions, _LightColor;
            TEXTURE2D(_SheetSpriteUvs); SAMPLER(sampler_SheetSpriteUvs);
            float _SheetSpriteCount;
            float4 _AppearanceEnabled, _SpeedRanges, _SheetGrid, _SheetTime, _SheetRow;
            float4 _TrailSettings, _TrailTextureScale;
            float _StateTime, _Elapsed, _MaxSize, _ApplyColorSpace;
            float4 _Gravity, _Color;
            struct Attributes {
                float3 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 motion : TEXCOORD1;
                float4 sizeRotation : TEXCOORD2;
                float4 ageRotation : TEXCOORD3;
                float2 trailTime : TEXCOORD4;
                float particleIndex : TEXCOORD5;
            };
            struct Varyings {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            #include "ParticleMotion.hlsl"
            float4 AppearanceSample(float value, float row) {
                return SAMPLE_TEXTURE2D_LOD(_Appearance, sampler_Appearance, float2((saturate(value) * 255 + .5) / 256, (row + .5) / 8), 0);
            }
            float SpeedParameter(float speed, float2 range) {
                return saturate((speed - range.x) / max(range.y - range.x, .000001));
            }
            float RotationRate(float speed, float random) {
                float2 rates = AppearanceSample(SpeedParameter(speed, _RotationSpeed.xy), 5).rg;
                return lerp(rates.x, rates.y, random);
            }
            float2 SheetUV(float2 uv, float age, float seconds, float speed, float random) {
                float frames = _SheetGrid.x * (_SheetGrid.z > .5 ? 1 : _SheetGrid.y);
                float t = _SheetTime.x == 1 ? SpeedParameter(speed, _SheetTime.zw) : age;
                float4 values = AppearanceSample(t, 4);
                float frame = lerp(values.b, values.a, random) * frames;
                frame += _SheetTime.x == 2 ? seconds * _SheetTime.y : lerp(values.r, values.g, random) * _SheetGrid.w * frames;
                frame = floor(frame);
                frame -= floor(frame / frames) * frames;
                if (_SheetSpriteCount > 0) {
                    float u = (frame + .5) / _SheetSpriteCount;
                    float4 basis = SAMPLE_TEXTURE2D_LOD(_SheetSpriteUvs, sampler_SheetSpriteUvs, float2(u, .25), 0);
                    float2 vertical = SAMPLE_TEXTURE2D_LOD(_SheetSpriteUvs, sampler_SheetSpriteUvs, float2(u, .75), 0).rg;
                    return basis.xy + uv.x * basis.zw + uv.y * vertical;
                }
                float column = fmod(frame, _SheetGrid.x);
                float row = _SheetGrid.z > .5 ? (_SheetRow.y > .5 ? min(floor(random * _SheetGrid.y), _SheetGrid.y - 1) : _SheetRow.x) : floor(frame / _SheetGrid.x);
                return (uv + float2(column, _SheetGrid.y - 1 - row)) / _SheetGrid.xy;
            }
            Varyings Vert(Attributes input) {
                Varyings output;
                float delta = max(0, _Elapsed - _StateTime);
                #if defined(PARTICLE_TRAILS)
                delta = max(0, _Elapsed - input.trailTime.x);
                #endif
                if (_DynamicsEnabled > .5) {
                    float2 stateUV = float2(fmod(input.particleIndex, 512) + .5, floor(input.particleIndex / 512) * 3 + .5) / _DynamicsSize.xy;
                    float4 p = SAMPLE_TEXTURE2D_LOD(_DynamicsTexture, sampler_DynamicsTexture, stateUV, 0);
                    float4 v = SAMPLE_TEXTURE2D_LOD(_DynamicsTexture, sampler_DynamicsTexture, stateUV + float2(0, 1 / _DynamicsSize.y), 0);
                    float4 m = SAMPLE_TEXTURE2D_LOD(_DynamicsTexture, sampler_DynamicsTexture, stateUV + float2(0, 2 / _DynamicsSize.y), 0);
                    input.position = p.xyz;
                    input.motion = float4(v.xyz, p.w);
                    input.ageRotation.y = v.w - p.w;
                    input.ageRotation.z = m.z;
                    input.sizeRotation.y = m.y;
                    delta = 0;
                }
                float presentDelta = delta;
                #if defined(PARTICLE_TRAILS)
                float trailLifetime = _TrailSettings.x * input.ageRotation.x;
                float trailHead = min(presentDelta, input.motion.w);
                float trailTail = max(presentDelta - trailLifetime, -input.ageRotation.y);
                // Stretch the whole strip over the available history, including birth and death.
                delta = lerp(trailHead, min(trailTail, trailHead), input.uv.x);
                #endif
                float age = saturate((input.ageRotation.y + delta) / max(input.ageRotation.x, 0.000001));
                float appearanceAge = age;
                #if defined(PARTICLE_TRAILS)
                // Native trails inherit the particle's current size and color along the
                // entire strip; only the centerline samples historical positions.
                appearanceAge = saturate((input.ageRotation.y + presentDelta) / max(input.ageRotation.x, .000001));
                #endif
                float sampleU = (appearanceAge * 255 + .5) / 256;
                float2 size = input.sizeRotation.xz * SAMPLE_TEXTURE2D_LOD(_Lifetime, sampler_Lifetime, float2(sampleU, .75), 0).r;
                
                float sine, cosine;
                float lifetime = max(input.ageRotation.x, .000001);
                float age0 = input.ageRotation.y / lifetime;
                float3 p0, v0, p1, v1;
                float r0, w0, r1, w1;
                EvaluateMotion(age0, lifetime, input.ageRotation.w, p0, v0, r0, w0);
                EvaluateMotion(age0 + delta / lifetime, lifetime, input.ageRotation.w, p1, v1, r1, w1);
                float3 velocity = input.motion.xyz + _Gravity.xyz * delta + v1 - v0;
                float speed = length(velocity);
                #if defined(PARTICLE_TRAILS)
                float3 presentPosition, presentVelocity;
                float presentRotation, presentRate;
                EvaluateMotion(appearanceAge, lifetime, input.ageRotation.w, presentPosition, presentVelocity, presentRotation, presentRate);
                speed = length(input.motion.xyz + _Gravity.xyz * presentDelta + presentVelocity - v0);
                #endif
                float2 sizeBySpeed = 1;
                if (_AppearanceEnabled.y > .5) {
                    float parameter = SpeedParameter(speed, _SpeedRanges.zw);
                    sizeBySpeed = lerp(AppearanceSample(parameter, 2).rg, AppearanceSample(parameter, 3).rg, input.ageRotation.w);
                }
                float2 finalSize = size * sizeBySpeed;
                float largestAxis = max(abs(finalSize.x), abs(finalSize.y));
                finalSize *= min(1, (2 * _MaxSize * unity_OrthoParams.y) / max(largestAxis, .000001));
                #if defined(PARTICLE_GLOW)
                float4 lightCurves = AppearanceSample(age, 6);
                float lightRange = _LightSettings.x * lerp(lightCurves.r, lightCurves.g, input.ageRotation.w);
                lightRange *= _LightOptions.y > .5 ? max(abs(size.x * sizeBySpeed.x), abs(size.y * sizeBySpeed.y)) : 1;
                finalSize = max(0, lightRange) * 2;
                #endif
                float2 corner = (input.uv - .5) * finalSize;
                float speedRotation = 0;
                #if !defined(PARTICLE_TRAILS)
                if (_RotationSpeed.z > .5) {
                    [unroll] for (int sample = 0; sample < 8; sample++) {
                        float time = delta * ((sample + .5) / 8);
                        float3 displacement, moduleVelocity;
                        float angle, rate;
                        EvaluateMotion(age0 + time / lifetime, lifetime, input.ageRotation.w, displacement, moduleVelocity, angle, rate);
                        speedRotation += RotationRate(length(input.motion.xyz + _Gravity.xyz * time + moduleVelocity - v0), input.ageRotation.w);
                    }
                    speedRotation = (speedRotation / 8 - RotationRate(length(input.motion.xyz), input.ageRotation.w)) * delta;
                }
                #endif
                sincos(-(input.sizeRotation.y + input.ageRotation.z * delta + r1 - r0 - w0 * delta + speedRotation), sine, cosine);
                corner = float2(cosine * corner.x - sine * corner.y, sine * corner.x + cosine * corner.y);
                float3 position = input.position + input.motion.xyz * delta + .5 * _Gravity.xyz * delta * delta + p1 - p0 - v0 * delta;
                #if defined(PARTICLE_TRAILS)
                float2 tangent = velocity.xy;
                tangent = dot(tangent, tangent) > .00000001 ? normalize(tangent) : float2(0, 1);
                float trailU = (input.uv.x * 255 + .5) / 256;
                float width = SAMPLE_TEXTURE2D_LOD(_TrailCurves, sampler_TrailCurves, float2(trailU, 2.5 / 3), 0).r;
                width *= _TrailSettings.y > .5 ? max(abs(size.x * sizeBySpeed.x), abs(size.y * sizeBySpeed.y)) : 1;
                position.xy += float2(-tangent.y, tangent.x) * (input.uv.y - .5) * width;
                #else
                position.xy += corner;
                #endif
                output.position = TransformObjectToHClip(position);
                output.uv = input.uv;
                output.color = input.color * SAMPLE_TEXTURE2D_LOD(_Lifetime, sampler_Lifetime, float2(sampleU, .25), 0);
                if (_AppearanceEnabled.x > .5) {
                    float parameter = _AppearanceEnabled.w > .5 ? input.ageRotation.w : SpeedParameter(speed, _SpeedRanges.xy);
                    output.color *= lerp(AppearanceSample(parameter, 0), AppearanceSample(parameter, 1), input.ageRotation.w);
                }
                #if !defined(PARTICLE_TRAILS)
                if (_AppearanceEnabled.z > .5) {
                    output.uv = SheetUV(output.uv, age, input.ageRotation.y + delta, speed, input.ageRotation.w);
                }
                #endif
                #if defined(PARTICLE_TRAILS)
                float presentAge = saturate((input.ageRotation.y + presentDelta) / max(input.ageRotation.x, .000001));
                float presentU = (presentAge * 255 + .5) / 256;
                output.color = (_TrailSettings.z > .5 ? output.color : float4(1, 1, 1, 1))
                    * SAMPLE_TEXTURE2D_LOD(_TrailCurves, sampler_TrailCurves, float2(trailU, .5 / 3), 0)
                    * SAMPLE_TEXTURE2D_LOD(_TrailCurves, sampler_TrailCurves, float2(presentU, 1.5 / 3), 0);
                output.uv *= _TrailTextureScale.xy;
                #endif
                #if defined(PARTICLE_GLOW)
                float intensity = max(0, _LightSettings.y * lerp(lightCurves.b, lightCurves.a, input.ageRotation.w));
                intensity *= _LightOptions.z > .5 ? output.color.a : 1;
                float distribution = _LightOptions.w > .5 ? input.ageRotation.w : frac(input.particleIndex * _LightSettings.z);
                bool selected = _LightOptions.w > .5 ? distribution < _LightSettings.z || _LightSettings.z >= 1 : distribution < _LightSettings.z;
                output.color = float4((_LightOptions.x > .5 ? output.color.rgb : float3(1, 1, 1)) * _LightColor.rgb, saturate(intensity));
                output.color.a *= selected && input.particleIndex < _LightSettings.w ? 1 : 0;
                output.uv = input.uv;
                #endif
                if (_ApplyColorSpace > .5) {
                    output.color.rgb = SRGBToLinear(output.color.rgb);
                }
                output.color *= _Color;
                #if defined(PARTICLE_TRAILS)
                output.color.a *= trailTail < trailHead
                    && (_TrailSettings.w < .5 || presentDelta < input.motion.w) ? 1 : 0;
                #else
                output.color.a *= delta < input.motion.w ? 1 : 0;
                #endif
                return output;
            }
            half4 Frag(Varyings input) : SV_Target {
                #if defined(PARTICLE_GLOW)
                float radial = saturate(1 - length(input.uv * 2 - 1));
                return half4(input.color.rgb, input.color.a * radial * radial);
                #else
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
                #endif
            }
            ENDHLSL
        }
    }
}
