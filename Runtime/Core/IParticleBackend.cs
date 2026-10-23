using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    internal interface IParticleBackend : System.IDisposable {

        public uint particleCount { get; }
        public uint bakedVertexCount { get; }
        public Bounds renderBounds { get; }
        public bool isAlive { get; }
        public bool requiresObservedParticles { get; }
        public void Initialize(GameObject prefab);
        public void SetSystemScale(Vector3 value);
        public void SetSimulationSpeed(float value);
        public void Play();
        public void Stop();
        public void Pause();
        public void Resume();
        public void StopEmitting();
        public void Tick();
        public void Draw(MeshGenerationContext context);

    }

}
