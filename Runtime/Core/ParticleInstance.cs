using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal sealed class ParticleInstance : System.IDisposable {

        internal const int EFFECT_LAYER = 31;
        private static readonly HashSet<int> slots = new();
        private int slot = -1;
        public GameObject gameObject { get; private set; }

        public void Create(GameObject prefab) {

            this.slot = 0;
            while (ParticleInstance.slots.Contains(this.slot) == true) {
                this.slot++;
            }

            ParticleInstance.slots.Add(this.slot);
            this.gameObject = Object.Instantiate(prefab);
            this.gameObject.name = $"[Particle UI] {prefab.name}";
            this.gameObject.hideFlags = Application.isPlaying == true ? HideFlags.DontSave : HideFlags.HideAndDontSave;
            this.gameObject.transform.position = new Vector3(10000 + this.slot * 100, 10000, 0);
            this.gameObject.transform.rotation = Quaternion.identity;
            using var transformsLease = UnityEngine.Pool.ListPool<Transform>.Get(out var transforms);
            this.gameObject.GetComponentsInChildren(true, transforms);
            foreach (var transform in transforms) {
                transform.gameObject.layer = ParticleInstance.EFFECT_LAYER;
                if (Application.isPlaying == false) {
                    transform.gameObject.hideFlags = HideFlags.HideAndDontSave;
                }
            }

        }

        internal void CreateEmpty(string name) {

            this.slot = 0;
            while (ParticleInstance.slots.Contains(this.slot) == true) {
                this.slot++;
            }

            ParticleInstance.slots.Add(this.slot);
            this.gameObject = new GameObject($"[Particle UI] {name}") {
                hideFlags = HideFlags.DontSave,
                layer = ParticleInstance.EFFECT_LAYER,
            };
            this.gameObject.transform.position = new Vector3(10000 + this.slot * 100, 10000, 0);

        }

        public void Dispose() {

            if (this.gameObject != null) {
                this.gameObject.SetActive(false);
                ParticleEnvironment.Destroy(this.gameObject);
                this.gameObject = null;
            }

            if (this.slot >= 0) {
                ParticleInstance.slots.Remove(this.slot);
                this.slot = -1;
            }

        }

    }

}
