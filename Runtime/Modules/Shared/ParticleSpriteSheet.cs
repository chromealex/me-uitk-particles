using UnityEngine;

namespace ME.UITK.Particles {

    internal static class ParticleSpriteSheet {

        internal static string Check(ParticleSystem.TextureSheetAnimationModule sheet) {

            if (sheet.spriteCount == 0) {
                return "Texture Sheet Animation requires at least one sprite";
            }

            Texture texture = null;
            for (var i = 0; i < sheet.spriteCount; i++) {
                var sprite = sheet.GetSprite(i);
                if (sprite == null || sprite.texture == null) {
                    return "Texture Sheet Animation contains a missing sprite or texture";
                }

                if (texture != null && texture != sprite.texture) {
                    return "Texture Sheet Animation sprites must share one texture atlas";
                }

                texture = sprite.texture;
                if (ParticleSpriteSheet.TryGetUvBasis(sprite, out _, out _, out _) == false) {
                    return "Texture Sheet Animation sprites must use Full Rect geometry without tight packing";
                }
            }

            return null;

        }

        internal static Texture GetTexture(ParticleSystem system, Material material) {

            var sheet = system.textureSheetAnimation;
            if (sheet.enabled == true && sheet.mode == ParticleSystemAnimationMode.Sprites && sheet.spriteCount > 0) {
                var sprite = sheet.GetSprite(0);
                if (sprite != null) {
                    return sprite.texture;
                }
            }

            return material != null ? material.mainTexture : null;

        }

        internal static bool TryGetUvBasis(Sprite sprite, out Vector2 origin, out Vector2 horizontal, out Vector2 vertical) {

            origin = Vector2.zero;
            horizontal = Vector2.zero;
            vertical = Vector2.zero;
            if (sprite.packed == true && sprite.packingMode == SpritePackingMode.Tight) {
                return false;
            }

            var vertices = sprite.vertices;
            var uvs = sprite.uv;
            if (vertices.Length != 4 || uvs.Length != 4) {
                return false;
            }

            var min = vertices[0];
            var max = min;
            for (var i = 1; i < vertices.Length; i++) {
                min = Vector2.Min(min, vertices[i]);
                max = Vector2.Max(max, vertices[i]);
            }

            if (max.x <= min.x || max.y <= min.y) {
                return false;
            }

            var corners = 0u;
            var diagonal = Vector2.zero;
            for (var i = 0; i < vertices.Length; i++) {
                var left = Mathf.Approximately(vertices[i].x, min.x);
                var bottom = Mathf.Approximately(vertices[i].y, min.y);
                if ((left == false && Mathf.Approximately(vertices[i].x, max.x) == false)
                    || (bottom == false && Mathf.Approximately(vertices[i].y, max.y) == false)) {
                    return false;
                }

                var corner = (left == true ? 0 : 1) + (bottom == true ? 0 : 2);
                corners |= 1u << corner;
                switch (corner) {
                    case 0:
                        origin = uvs[i];
                        break;
                    case 1:
                        horizontal = uvs[i];
                        break;
                    case 2:
                        vertical = uvs[i];
                        break;
                    case 3:
                        diagonal = uvs[i];
                        break;
                }
            }

            horizontal -= origin;
            vertical -= origin;
            return corners == 15u && (origin + horizontal + vertical - diagonal).sqrMagnitude < .000001f;

        }

    }

}
