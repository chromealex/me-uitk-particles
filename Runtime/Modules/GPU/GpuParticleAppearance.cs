using UnityEngine;

namespace ME.UITK.Particles {

    internal sealed class GpuParticleAppearance : System.IDisposable {

        private readonly Texture2D texture;
        private readonly Texture2D spriteUvs;
        private readonly Vector2[] rotationRates = new Vector2[256];
        private readonly Vector2 rotationRange;
        internal bool rotationEnabled { get; private set; }
        internal float lightRangeBound { get; private set; }
        internal float sizeBound { get; private set; } = 1f;

        internal static string Check(ParticleSystem system) {

            if (system.rotationBySpeed.enabled == true && system.rotationBySpeed.separateAxes == true) {
                return "GPU Rotation by Speed supports Z rotation only (disable Separate Axes)";
            }

            var sheet = system.textureSheetAnimation;
            if (sheet.enabled == true) {
                if ((sheet.uvChannelMask & UnityEngine.Rendering.UVChannelFlags.UV0) == 0) {
                    return "GPU Texture Sheet Animation requires UV0";
                }

                if (float.IsFinite(sheet.fps) == false || sheet.fps < 0f) {
                    return "GPU Texture Sheet Animation requires finite nonnegative FPS";
                }

                if (sheet.mode == ParticleSystemAnimationMode.Sprites) {
                    if (sheet.spriteCount > SystemInfo.maxTextureSize) {
                        return "GPU sprite frame count exceeds the device texture size limit";
                    }

                    return ParticleSpriteSheet.Check(sheet);
                }

                if (sheet.rowMode == ParticleSystemAnimationRowMode.MeshIndex
                    && sheet.animation == ParticleSystemAnimationType.SingleRow) {
                    return "GPU Texture Sheet Animation requires Custom or Random row selection";
                }

                if (sheet.numTilesX < 1 || sheet.numTilesY < 1 || sheet.numTilesX > 256 || sheet.numTilesY > 256) {
                    return "GPU Texture Sheet Animation requires 1-256 tiles per axis";
                }
            }

            return null;

        }

        internal GpuParticleAppearance(ParticleSystem system, Material material) {

            var rotation = system.rotationBySpeed;
            this.rotationEnabled = rotation.enabled;
            this.rotationRange = rotation.range;
            material.SetVector("_RotationSpeed", new Vector4(rotation.range.x, rotation.range.y, rotation.enabled == true ? 1f : 0f, 0f));
            var color = system.colorBySpeed;
            var size = system.sizeBySpeed;
            var sheet = system.textureSheetAnimation;
            var lights = system.lights;
            material.SetVector("_AppearanceEnabled", new Vector4(color.enabled == true ? 1f : 0f, size.enabled == true ? 1f : 0f,
                sheet.enabled == true ? 1f : 0f, color.color.mode == ParticleSystemGradientMode.RandomColor ? 1f : 0f));
            if (color.enabled == false && size.enabled == false && sheet.enabled == false && rotation.enabled == false && lights.enabled == false) {
                return;
            }

            material.SetVector("_SpeedRanges", new Vector4(color.range.x, color.range.y, size.range.x, size.range.y));
            material.SetVector("_SheetGrid", new Vector4(sheet.numTilesX, sheet.numTilesY,
                sheet.animation == ParticleSystemAnimationType.SingleRow ? 1f : 0f, sheet.cycleCount));
            material.SetVector("_SheetTime", new Vector4(sheet.timeMode == ParticleSystemAnimationTimeMode.Speed ? 1f
                : sheet.timeMode == ParticleSystemAnimationTimeMode.FPS ? 2f : 0f, sheet.fps, sheet.speedRange.x, sheet.speedRange.y));
            material.SetVector("_SheetRow", new Vector4(sheet.rowIndex, sheet.rowMode == ParticleSystemAnimationRowMode.Random ? 1f : 0f, 0f, 0f));
            if (sheet.enabled == true && sheet.mode == ParticleSystemAnimationMode.Sprites) {
                material.mainTexture = ParticleSpriteSheet.GetTexture(system, material);
                material.SetVector("_SheetGrid", new Vector4(sheet.spriteCount, 1f, 0f, sheet.cycleCount));
                material.SetFloat("_SheetSpriteCount", sheet.spriteCount);
                this.spriteUvs = new Texture2D(sheet.spriteCount, 2, TextureFormat.RGBAFloat, false, true) {
                    name = "GPU sprite atlas UVs",
                    hideFlags = HideFlags.HideAndDontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point,
                };
                for (var i = 0; i < sheet.spriteCount; i++) {
                    ParticleSpriteSheet.TryGetUvBasis(sheet.GetSprite(i), out var origin, out var horizontal, out var vertical);
                    this.spriteUvs.SetPixel(i, 0, new Color(origin.x, origin.y, horizontal.x, horizontal.y));
                    this.spriteUvs.SetPixel(i, 1, new Color(vertical.x, vertical.y, 0f, 0f));
                }

                this.spriteUvs.Apply(false, true);
                material.SetTexture("_SheetSpriteUvs", this.spriteUvs);
            }

            this.texture = new Texture2D(256, 8, TextureFormat.RGBAFloat, false, true) {
                name = "GPU appearance curves", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
            };
            for (var i = 0; i < 256; i++) {
                var value = i / 255f;
                for (var endpoint = 0; endpoint < 2; endpoint++) {
                    var tint = color.enabled == false ? Color.white : color.color.mode == ParticleSystemGradientMode.RandomColor
                        ? color.color.gradient.Evaluate(value) : color.color.Evaluate(value, endpoint);
                    var width = size.enabled == false ? 1f : (size.separateAxes == true ? size.x : size.size).Evaluate(value, endpoint);
                    var height = size.enabled == false ? 1f : (size.separateAxes == true ? size.y : size.size).Evaluate(value, endpoint);
                    this.sizeBound = Mathf.Max(this.sizeBound, Mathf.Abs(width), Mathf.Abs(height));
                    this.texture.SetPixel(i, endpoint, tint);
                    this.texture.SetPixel(i, 2 + endpoint, new Color(width, height, 0f, 0f));
                }

                if (lights.enabled == true) {
                    var rangeMin = lights.range.Evaluate(value, 0f);
                    var rangeMax = lights.range.Evaluate(value, 1f);
                    this.lightRangeBound = Mathf.Max(this.lightRangeBound, Mathf.Abs(rangeMin), Mathf.Abs(rangeMax));
                    this.texture.SetPixel(i, 6, new Color(rangeMin, rangeMax, lights.intensity.Evaluate(value, 0f), lights.intensity.Evaluate(value, 1f)));
                }

                this.rotationRates[i] = rotation.enabled == true ? new Vector2(rotation.z.Evaluate(value, 0f), rotation.z.Evaluate(value, 1f)) : Vector2.zero;
                this.texture.SetPixel(i, 5, new Color(this.rotationRates[i].x, this.rotationRates[i].y, 0f, 0f));
                this.texture.SetPixel(i, 4, new Color(sheet.frameOverTime.Evaluate(value, 0f), sheet.frameOverTime.Evaluate(value, 1f),
                    sheet.startFrame.Evaluate(0f, 0f), sheet.startFrame.Evaluate(0f, 1f)));
            }

            this.lightRangeBound *= lights.enabled == true ? lights.light.range : 0f;
            this.texture.Apply(false, true);
            material.SetTexture("_Appearance", this.texture);

        }

        internal float RotationRate(float speed, float random) {

            if (this.rotationEnabled == false) {
                return 0f;
            }

            var index = Mathf.Clamp01((speed - this.rotationRange.x) / Mathf.Max(this.rotationRange.y - this.rotationRange.x, .000001f)) * 255f;
            var lo = Mathf.FloorToInt(index);
            var rates = Vector2.Lerp(this.rotationRates[lo], this.rotationRates[Mathf.Min(lo + 1, 255)], index - lo);
            return Mathf.Lerp(rates.x, rates.y, random);

        }

        public void Dispose() {

            ParticleEnvironment.Destroy(this.texture);
            ParticleEnvironment.Destroy(this.spriteUvs);

        }

    }

}
