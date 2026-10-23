using System.Collections.Generic;
using UnityEngine;

namespace ME.UITK.Particles {

    internal static class ParticleAtlasManager {

        private static readonly List<ParticleAtlasPresenter> entries = new();
        private static readonly List<ParticleAtlasPage> pages = new();
        internal static uint entryCount => (uint)ParticleAtlasManager.entries.Count;
        private const uint MAX_CACHED_PAGES = 2;
        private static int refreshedFrame = -1;
        internal static int pageCount {
            get {
                var count = 0;
                foreach (var page in ParticleAtlasManager.pages) {
                    if (page.empty == false) {
                        count++;
                    }
                }

                return count;
            }
        }

        internal static void Add(ParticleAtlasPresenter entry) {

            ParticleAtlasManager.entries.Add(entry);
            ParticleAtlasManager.Refresh(true);

        }

        internal static void Remove(ParticleAtlasPresenter entry) {

            ParticleAtlasManager.entries.Remove(entry);
            if (entry.page != null) {
                entry.page.Remove(entry);
                entry.page = null;
            }

            ParticleAtlasManager.Refresh(true);

        }

        internal static void Refresh(bool force) {

            if (force == false && Application.isPlaying == true && ParticleAtlasManager.refreshedFrame == Time.frameCount) {
                return;
            }

            ParticleAtlasManager.refreshedFrame = Time.frameCount;
            foreach (var entry in ParticleAtlasManager.entries) {
                entry.UpdateBounds();
            }

            foreach (var entry in ParticleAtlasManager.entries) {
                var compatibleCount = 0u;
                foreach (var candidate in ParticleAtlasManager.entries) {
                    if (candidate.playing == true && candidate.eligible == true && candidate.span == entry.span && candidate.tilePixels == entry.tilePixels) {
                        compatibleCount++;
                    }
                }

                // Auto only replaces texture fallback, never the established Shader/Mesh paths.
                // Require a full page to enter; retain it down to two effects to avoid churn.
                var threshold = entry.page != null ? 2u : 4u;
                var useAtlas = entry.eligible == true && (entry.automatic == false
                    || entry.playing == true && compatibleCount >= threshold
                    || entry.playing == false && entry.page != null);
                if (useAtlas == false) {
                    entry.UseFallback();
                    continue;
                }

                if (entry.page != null && (entry.page.span != entry.span || entry.page.tilePixels != entry.tilePixels)) {
                    entry.page.Remove(entry);
                    entry.page = null;
                }

                if (entry.page != null) {
                    continue;
                }

                ParticleAtlasPage page = null;
                foreach (var candidate in ParticleAtlasManager.pages) {
                    if (candidate.span == entry.span && candidate.tilePixels == entry.tilePixels && candidate.hasSpace == true) {
                        page = candidate;
                        break;
                    }
                }

                if (page == null) {
                    foreach (var candidate in ParticleAtlasManager.pages) {
                        if (candidate.empty == true && candidate.tilePixels == entry.tilePixels) {
                            candidate.SetSpan(entry.span);
                            page = candidate;
                            break;
                        }
                    }
                }

                if (page == null) {
                    page = new ParticleAtlasPage(entry.span, entry.tilePixels);
                    ParticleAtlasManager.pages.Add(page);
                }

                page.Add(entry);
            }

            // Bound retained GPU memory and release everything after the last owner leaves.
            var cachedPages = 0u;
            for (var i = ParticleAtlasManager.pages.Count - 1; i >= 0; i--) {
                var page = ParticleAtlasManager.pages[i];
                page.UpdateEnabled();
                if (page.empty == true) {
                    if (ParticleAtlasManager.entries.Count == 0 || cachedPages >= MAX_CACHED_PAGES) {
                        page.Dispose();
                        ParticleAtlasManager.pages.RemoveAt(i);
                    } else {
                        cachedPages++;
                    }
                }
            }

        }

    }

}
