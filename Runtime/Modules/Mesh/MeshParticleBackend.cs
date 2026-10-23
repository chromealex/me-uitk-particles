using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Jobs;

namespace ME.UITK.Particles {

    internal class MeshParticleBackend : NativeParticleBackend {

        private readonly List<Mesh> meshes = new();
        private readonly List<ParticleSystem> rendererSystems = new();
        private Vertex[] convertedVertices = System.Array.Empty<Vertex>();
        private readonly List<Vector3> vertices = new();
        private readonly List<Color32> colors = new();
        private readonly List<Vector2> uvs = new();
        private readonly List<int> indices = new();
        private Camera effectCamera;
        private static readonly int colorProperty = Shader.PropertyToID("_Color");
        private const int MAX_MESH_CHUNK_SIZE = 60000;

        public MeshParticleBackend(ParticleElement owner) : base(owner) {

        }

        public override void Initialize(GameObject prefab) {

            base.Initialize(prefab);
            var maximumVertices = 0;
            foreach (var system in this.systems) {
                maximumVertices = Mathf.Max(maximumVertices, checked(system.main.maxParticles * 4));
            }

            this.EnsureVertexCapacity(maximumVertices);
            this.meshes.Capacity = Mathf.Max(this.meshes.Capacity, this.renderers.Length);
            foreach (var renderer in this.renderers) {
                renderer.enabled = false;
                var mesh = new Mesh { name = "Particle UI bake", hideFlags = HideFlags.HideAndDontSave, };
                mesh.MarkDynamic();
                this.meshes.Add(mesh);
                this.rendererSystems.Add(renderer.GetComponent<ParticleSystem>());
            }

            var cameraObject = new GameObject("[Particle UI] Camera") { hideFlags = HideFlags.HideAndDontSave, };
            this.effectCamera = cameraObject.AddComponent<Camera>();
            this.effectCamera.enabled = false;
            this.effectCamera.orthographic = true;
            this.effectCamera.nearClipPlane = .1f;
            this.effectCamera.farClipPlane = 40;
            cameraObject.transform.position = this.instance.transform.position + Vector3.back * 10;

        }

        public override void Dispose() {

            foreach (var mesh in this.meshes) {
                ParticleEnvironment.Destroy(mesh);
            }

            this.meshes.Clear();
            this.rendererSystems.Clear();
            if (this.effectCamera != null) {
                ParticleEnvironment.Destroy(this.effectCamera.gameObject);
                this.effectCamera = null;
            }

            if (this.convertedVertices.Length != 0) {
                System.Buffers.ArrayPool<Vertex>.Shared.Return(this.convertedVertices);
                this.convertedVertices = System.Array.Empty<Vertex>();
            }

            base.Dispose();

        }

        public override void Draw(MeshGenerationContext context) {

            this.bakedVertexCount = 0;
            if (this.instance == null || this.owner.contentRect.width <= 0 || this.owner.contentRect.height <= 0) {
                return;
            }

            var center = this.owner.presentationOrigin;
            for (var r = 0; r < this.renderers.Length; r++) {
                var renderer = this.renderers[r];
                if (renderer == null || renderer.gameObject.activeInHierarchy == false) {
                    continue;
                }

                var mesh = this.meshes[r];
                renderer.BakeMesh(mesh, this.effectCamera, (ParticleSystemBakeMeshOptions)0);
                var matrix = Matrix4x4.Translate(-this.instance.transform.position) * renderer.transform.localToWorldMatrix;
                var system = this.rendererSystems[r];
                this.DrawBakedMesh(context, mesh, matrix, center, renderer.sharedMaterial,
                    ParticleSpriteSheet.GetTexture(system, renderer.sharedMaterial));
                if (system != null && system.trails.enabled == true) {
                    // Trails can outlive their particles, so bake even when the billboard mesh is empty.
                    mesh.Clear();
                    renderer.BakeTrailsMesh(mesh, this.effectCamera, (ParticleSystemBakeMeshOptions)0);
                    var trailMatrix = system.trails.worldSpace == true
                        ? Matrix4x4.Translate(-this.instance.transform.position) : matrix;
                    this.DrawBakedMesh(context, mesh, trailMatrix, center, renderer.trailMaterial, renderer.trailMaterial != null ? renderer.trailMaterial.mainTexture : null);
                }
            }

        }

        private void DrawBakedMesh(MeshGenerationContext context, Mesh mesh, Matrix4x4 matrix, Vector2 center, Material material, Texture texture) {

            if (mesh.vertexCount == 0 || material == null) {
                return;
            }

            this.EnsureVertexCapacity(mesh.vertexCount);
            mesh.GetVertices(this.vertices);
            mesh.GetColors(this.colors);
            mesh.GetUVs(0, this.uvs);
            mesh.GetTriangles(this.indices, 0);
            this.NormalizeWinding(matrix);
            if (this.owner.presentationMirrored == true) {
                for (var i = 0; i < this.indices.Count; i += 3) {
                    (this.indices[i + 1], this.indices[i + 2]) = (this.indices[i + 2], this.indices[i + 1]);
                }
            }
            // UI Toolkit uses ushort indices. Split by triangles to support large prefabs safely.
            var tint = material.HasProperty(colorProperty) == true ? material.color : Color.white;
            var linearColorSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
            if (linearColorSpace == true) {
                // Match the material color supplied to the native particle shader.
                tint = tint.linear;
            }

            if (this.owner.useJobs == true && (uint)this.vertices.Count >= this.owner.jobVertexThreshold) {
                this.DrawJobMesh(context, matrix, center, tint, linearColorSpace, texture);
                return;
            }

            var hasColors = this.colors.Count == this.vertices.Count;
            var hasUvs = this.uvs.Count == this.vertices.Count;
            var previousColor = new Color32();
            var convertedColor = new Color32();
            for (var i = 0; i < this.vertices.Count; i++) {
                var sourceColor = hasColors == true ? this.colors[i] : (Color32)Color.white;
                if (i == 0 || sourceColor.r != previousColor.r || sourceColor.g != previousColor.g
                    || sourceColor.b != previousColor.b || sourceColor.a != previousColor.a) {
                    var color = (Color)sourceColor * tint;
                    if (linearColorSpace == true) {
                        // BakeMesh has already applied the renderer setting; UITK expects sRGB.
                        color = color.gamma;
                    }

                    convertedColor = (Color32)(color * this.owner.tint);
                    previousColor = sourceColor;
                }

                var p = matrix.MultiplyPoint3x4(this.vertices[i]);
                this.convertedVertices[i] = new Vertex {
                    position = this.owner.PresentationPosition(new Vector3(center.x + p.x * this.owner.effectivePixelsPerUnit, center.y - p.y * this.owner.effectivePixelsPerUnit, Vertex.nearZ)),
                    tint = convertedColor,
                    uv = hasUvs == true ? this.uvs[i] : Vector2.zero,
                };
            }

            if (this.vertices.Count <= MAX_MESH_CHUNK_SIZE && this.indices.Count <= MAX_MESH_CHUNK_SIZE) {
                var data = context.Allocate(this.vertices.Count, this.indices.Count, texture);
                for (var i = 0; i < this.vertices.Count; i++) {
                    data.SetNextVertex(this.convertedVertices[i]);
                }

                foreach (var index in this.indices) {
                    data.SetNextIndex((ushort)index);
                }

                this.bakedVertexCount += (uint)this.vertices.Count;
            } else {
                // Large meshes retain triangle-aligned chunks within UITK's ushort limit.
                for (var start = 0; start < this.indices.Count; start += MAX_MESH_CHUNK_SIZE) {
                    var count = Mathf.Min(MAX_MESH_CHUNK_SIZE, this.indices.Count - start);
                    var data = context.Allocate(count, count, texture);
                    for (var i = 0; i < count; i++) {
                        data.SetNextVertex(this.convertedVertices[this.indices[start + i]]);
                        data.SetNextIndex((ushort)i);
                    }

                    this.bakedVertexCount += (uint)count;
                }
            }

        }

        private void NormalizeWinding(Matrix4x4 matrix) {

            // UITK's stencil clipping requires clockwise triangles in UI coordinates.
            // Baked trails can contain both orientations; projecting also flips Y.
            for (var i = 0; i < this.indices.Count; i += 3) {
                var a = matrix.MultiplyPoint3x4(this.vertices[this.indices[i]]);
                var b = matrix.MultiplyPoint3x4(this.vertices[this.indices[i + 1]]);
                var c = matrix.MultiplyPoint3x4(this.vertices[this.indices[i + 2]]);
                var cross = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
                if (cross > 0f) {
                    var index = this.indices[i + 1];
                    this.indices[i + 1] = this.indices[i + 2];
                    this.indices[i + 2] = index;
                }
            }

        }

        private void DrawJobMesh(MeshGenerationContext context, Matrix4x4 matrix, Vector2 center, Color tint, bool linearColorSpace, Texture texture) {

            var indexed = this.vertices.Count <= MAX_MESH_CHUNK_SIZE && this.indices.Count <= MAX_MESH_CHUNK_SIZE;
            var hasColors = this.colors.Count == this.vertices.Count;
            var hasUvs = this.uvs.Count == this.vertices.Count;
            for (var start = 0; start < this.indices.Count; start += MAX_MESH_CHUNK_SIZE) {
                var indexCount = Mathf.Min(MAX_MESH_CHUNK_SIZE, this.indices.Count - start);
                var vertexCount = indexed == true ? this.vertices.Count : indexCount;
                context.AllocateTempMesh(vertexCount, indexCount, out var outputVertices, out var outputIndices);
                for (var i = 0; i < vertexCount; i++) {
                    var source = indexed == true ? i : this.indices[start + i];
                    outputVertices[i] = new Vertex {
                        position = this.vertices[source],
                        tint = hasColors == true ? this.colors[source] : (Color32)Color.white,
                        uv = hasUvs == true ? this.uvs[source] : Vector2.zero,
                    };
                }

                for (var i = 0; i < indexCount; i++) {
                    outputIndices[i] = (ushort)(indexed == true ? this.indices[start + i] : i);
                }

                var job = new ParticleVertexJob {
                    vertices = outputVertices,
                    matrix = matrix,
                    presentationMatrix = this.owner.presentationMatrix,
                    applyPresentationTransform = this.owner.renderOnTop,
                    center = center,
                    tint = tint,
                    presentationTint = this.owner.tint,
                    pixelsPerUnit = this.owner.effectivePixelsPerUnit,
                    linearColorSpace = linearColorSpace,
                };
                context.DrawMesh(outputVertices, outputIndices, texture);
                context.AddMeshGenerationJob(job.Schedule());
                this.bakedVertexCount += (uint)vertexCount;
            }

        }

        private void EnsureVertexCapacity(int count) {

            if (this.convertedVertices.Length >= count) {
                return;
            }

            var capacity = Mathf.NextPowerOfTwo(count);
            if (capacity <= 0) {
                capacity = count;
            }

            var indexCapacity = checked((int)((long)capacity * 3 / 2));
            var previous = this.convertedVertices;
            this.convertedVertices = System.Buffers.ArrayPool<Vertex>.Shared.Rent(capacity);
            if (previous.Length != 0) {
                System.Buffers.ArrayPool<Vertex>.Shared.Return(previous);
            }

            this.vertices.Capacity = Mathf.Max(this.vertices.Capacity, capacity);
            this.colors.Capacity = Mathf.Max(this.colors.Capacity, capacity);
            this.uvs.Capacity = Mathf.Max(this.uvs.Capacity, capacity);
            this.indices.Capacity = Mathf.Max(this.indices.Capacity, indexCapacity);

        }


    }

}
