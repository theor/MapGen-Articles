using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Generation
{
    public class MeshBuilder
    {
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _indices = new List<int>();
        private readonly List<Color32> _colors = new List<Color32>();
        private readonly Mesh _mesh;

        public MeshBuilder(GameObject go)
        {
            _mesh = SetupMesh(go);
        }

        public void Generate(Action<MeshBuilder> gen)
        {
            gen(this);
            _mesh.indexFormat = IndexFormat.UInt32;
            _mesh.SetVertices(_vertices);
            _mesh.SetIndices(_indices, MeshTopology.Triangles, 0);
            _mesh.SetColors(_colors);
            _mesh.RecalculateNormals();
            _mesh.RecalculateTangents();
            _mesh.RecalculateBounds();
        }

        private Mesh SetupMesh(GameObject go)
        {
            var mf = go.GetComponent<MeshFilter>();
            Mesh m;
            if (mf.sharedMesh)
            {
                m = mf.sharedMesh;
                m.Clear();
            }
            else
            {
                m = new Mesh();
                mf.sharedMesh = m;
            }

            return m;
        }


        public void
            AddTriangle(TriangleStorage.Triangle triangle,
                TriangleStorage storage,
                float3 y, Color c1, Color c2, Color c3)
        {
            var i = _vertices.Count;
            _vertices.Add(storage.F3(triangle.V1, y.x));
            _vertices.Add(storage.F3(triangle.V2, y.y));
            _vertices.Add(storage.F3(triangle.V3, y.z));
            _colors.Add(c1);
            _colors.Add(c2);
            _colors.Add(c3);
            _indices.Add(i);
            _indices.Add(i + 1);
            _indices.Add(i + 2);
        }

        public void AddTriangle(float3 v1, float3 v2,
            float3 v3,
            float y, Color color)
        {
            var i = _vertices.Count;
            v1.y = v2.y = v3.y = y;
            _vertices.Add(v1);
            _vertices.Add(v2);
            _vertices.Add(v3);
            for (int j = 0; j < 3; j++) _colors.Add(color);
            _indices.Add(i);
            _indices.Add(i + 1);
            _indices.Add(i + 2);
        }

        private void AddQuad(Vector2 point,
            float f, float y = 0,
            Color32 color = default)
        {
            var i = _vertices.Count;
            var dx = new Vector3(f / 2, 0, 0);
            var dz = new Vector3(0, 0, f / 2);
            var center = new Vector3(point.x, y, point.y);
            _vertices.Add(center - dx - dz);
            _vertices.Add(center - dx + dz);
            _vertices.Add(center + dx - dz);
            _vertices.Add(center + dx + dz);
            for (int j = 0; j < 4; j++) _colors.Add(color);
            _indices.Add(i);
            _indices.Add(i + 1);
            _indices.Add(i + 2);
            _indices.Add(i + 1);
            _indices.Add(i + 3);
            _indices.Add(i + 2);
        }
    }
}