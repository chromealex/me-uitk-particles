// <summary>
// Converts particle mesh positions and colors into UI Toolkit vertices using a Burst-compiled job.
// </summary>
// Author: Alex Silaev (https://chromealex.com)

using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.UIElements;

namespace ME.UITK.Particles {

    [BurstCompile(CompileSynchronously = true)]
    internal struct ParticleVertexJob : IJob {

        public NativeSlice<Vertex> vertices;
        public Matrix4x4 matrix;
        public Matrix4x4 presentationMatrix;
        public bool applyPresentationTransform;
        public Vector2 center;
        public Color tint;
        public Color presentationTint;
        public float pixelsPerUnit;
        public bool linearColorSpace;

        public void Execute() {

            var previous = new Color32();
            var converted = new Color32();
            for (var i = 0; i < this.vertices.Length; i++) {
                var vertex = this.vertices[i];
                var source = vertex.tint;
                if (i == 0 || source.r != previous.r || source.g != previous.g || source.b != previous.b || source.a != previous.a) {
                    var color = (Color)source * this.tint;
                    if (this.linearColorSpace == true) {
                        color = new Color(LinearToGamma(color.r), LinearToGamma(color.g), LinearToGamma(color.b), color.a);
                    }

                    converted = (Color32)(color * this.presentationTint);
                    previous = source;
                }

                var p = this.matrix.MultiplyPoint3x4(vertex.position);
                vertex.position = new Vector3(this.center.x + p.x * this.pixelsPerUnit, this.center.y - p.y * this.pixelsPerUnit, Vertex.nearZ);
                if (this.applyPresentationTransform == true) {
                    vertex.position = this.presentationMatrix.MultiplyPoint3x4(vertex.position);
                    vertex.position.z = Vertex.nearZ;
                }
                vertex.tint = converted;
                this.vertices[i] = vertex;
            }

        }

        private static float LinearToGamma(float value) {

            if (value <= 0f) {
                return 0f;
            }

            if (value <= .0031308f) {
                return 12.92f * value;
            }

            if (value < 1f) {
                return 1.055f * MathF.Pow(value, 1f / 2.4f) - .055f;
            }

            return MathF.Pow(value, 1f / 2.2f);

        }

    }

}
