using System;
using System.IO;
// using MapGen.Jobs;
// using NativeQuadTree;
// using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
// using Unity.Profiling;
using UnityEngine;
using UnityEngine.Assertions;


namespace Generation
{
    public struct TriangleStorage
    {
        public struct Triangle
        {
            public readonly ushort V1;
            public readonly ushort V2;
            public readonly ushort V3;
            public readonly float3 CircumCircleCenter;
            public readonly float CircumCircleRadiusSquared;
            public int T1, T2, T3;
            public bool IsDeleted;

            public Triangle(TriangleStorage storage, ushort v1, ushort v2, ushort v3)
            {
                V1 = v1;
                V2 = v2;
                V3 = v3;
                T1 = -1;
                T2 = -1;
                T3 = -1;
                IsDeleted = false;

                Geometry.CircumCircle(storage.F3(v1), storage.F3(v2), storage.F3(v3),
                    out CircumCircleCenter, out var circleRadius);
                CircumCircleRadiusSquared = circleRadius * circleRadius;
            }

            public Edge Edge1 => new Edge(V1, V2);
            public Edge Edge2 => new Edge(V2, V3);
            public Edge Edge3 => new Edge(V3, V1);

            // public bool Contains(int v1, int v2, int v3)
            // {
            //     if (V1 == v1 && V2 == v2 && V3 == v3)
            //         return true;
            //     if (V1 == v2 && V2 == v3 && V3 == v1)
            //         return true;
            //     if (V1 == v3 && V2 == v1 && V3 == v2)
            //         return true;
            //     return false;
            // }

            public bool ContainsVertex(int v) => V1 == v || V2 == v || V3 == v;

            // public static bool GetReversedEdgeIndex(Triangle triangle, Edge neighourTriangleEdge, out int index)
            // {
            //     if (neighourTriangleEdge.A == triangle.Edge1.B && neighourTriangleEdge.B == triangle.Edge1.A)
            //     {
            //         index = 0;
            //         return true;
            //     }
            //
            //     if (neighourTriangleEdge.A == triangle.Edge2.B && neighourTriangleEdge.B == triangle.Edge2.A)
            //     {
            //         index = 1;
            //         return true;
            //     }
            //
            //     if (neighourTriangleEdge.A == triangle.Edge3.B && neighourTriangleEdge.B == triangle.Edge3.A)
            //     {
            //         index = 2;
            //         return true;
            //     }
            //
            //     index = default;
            //     return false;
            // }
            //
            // public int NeighbourIndex(int triId)
            // {
            //     switch (triId)
            //     {
            //         case 1: return T1;
            //         case 2: return T2;
            //         case 3: return T3;
            //         default: throw new InvalidDataException("TriId must be 1,2 or 3");
            //     }
            // }
        }

        public struct Vertex
        {
            public float2 Position;
            public int TriangleIndex;
        }

        public struct EdgeRef
        {
            public readonly int TriangleIndex;
            public readonly int EdgeIndex;

            public EdgeRef(int triangleIndex, int edgeIndex)
            {
                TriangleIndex = triangleIndex;
                EdgeIndex = edgeIndex;
            }
        }

        public NativeArray<Vertex> Points;
        public NativeList<Triangle> Triangles;
        public NativeQueue<int> DeletedTriangles1;
        public NativeQueue<int> DeletedTriangles2;

        public TriangleStorage(int pointsLength)
        {
            Points = new NativeArray<Vertex>(pointsLength, Allocator.Persistent);
            Triangles = new NativeList<Triangle>(Allocator.Persistent);
            DeletedTriangles1 = new NativeQueue<int>(Allocator.Persistent);
            DeletedTriangles2 = new NativeQueue<int>(Allocator.Persistent);
            pool1 = true;
        }

        public bool IsCreated => Points.IsCreated;

        public void Dispose()
        {
            Debug.Log("TriStorage dispose");
            if (Points.IsCreated)
                Points.Dispose();
            if (Triangles.IsCreated)
                Triangles.Dispose();
            if (DeletedTriangles1.IsCreated)
                DeletedTriangles1.Dispose();
            if (DeletedTriangles2.IsCreated)
                DeletedTriangles2.Dispose();
        }


        public void AddVertices(NativeArray<float2> float2s)
        {
            for (int i = 0; i < float2s.Length; i++)
            {
                Points[i] = new Vertex {Position = float2s[i], TriangleIndex = -1};
            }
        }

        public void AddVertex(int i, float2 position)
        {
            Points[i] = new Vertex {Position = position, TriangleIndex = -1};
        }

        public int AddTriangle(ushort v1, ushort v2, ushort v3)
        {
            unsafe
            {
                int idx;
                if ((pool1 ? DeletedTriangles1 : DeletedTriangles2).TryDequeue(out idx))
                {
                    Triangle* triPtr = (Triangle*) Triangles.GetUnsafePtr();
                    Triangle* triangle = triPtr + idx;
                    var t = new Triangle(this, v1, v2, v3);
                    UnsafeUtility.CopyStructureToPtr(ref t, triangle);
                }
                else
                {
                    var t = new Triangle(this, v1, v2, v3);
                    Triangles.Add(t);
                    idx = Triangles.Length - 1;
                }

                var unsafePtr = (Vertex*) Points.GetUnsafePtr();
                (unsafePtr + v1)->TriangleIndex = idx;
                (unsafePtr + v2)->TriangleIndex = idx;
                (unsafePtr + v3)->TriangleIndex = idx;
                return idx;
            }
        }

        public int AddTriangle(EdgeRef neighborEdge, ushort ip)
        {
            var deletedTriangle = Triangles[neighborEdge.TriangleIndex];
            Edge e;
            int i;
            Triangle newTriangle;
            switch (neighborEdge.EdgeIndex)
            {
                case 0:
                    e = deletedTriangle.Edge1;
                    i = AddTriangle(e.A, e.B, ip);
                    newTriangle = Triangles[i];
                    newTriangle.T1 = deletedTriangle.T1;
                    Triangles[i] = newTriangle;
                    SetNeighbour(newTriangle.T1, e.A, e.B, i); // !
                    break;
                case 1:
                    e = deletedTriangle.Edge2;
                    i = AddTriangle(e.A, e.B, ip);
                    newTriangle = Triangles[i];
                    newTriangle.T1 = deletedTriangle.T2;
                    Triangles[i] = newTriangle;
                    SetNeighbour(newTriangle.T1, e.A, e.B, i); // !
                    break;
                case 2:
                    e = deletedTriangle.Edge3;
                    i = AddTriangle(e.A, e.B, ip);
                    newTriangle = Triangles[i];
                    newTriangle.T1 = deletedTriangle.T3;
                    Triangles[i] = newTriangle;
                    SetNeighbour(newTriangle.T1, e.A, e.B, i); // !
                    break;
                default: throw new InvalidDataException();
            }

            return i;
        }

        public void RemoveTriangle(int i)
        {
            var triangle = Triangles[i];
            triangle.IsDeleted = true;
            Triangles[i] = triangle;
            if (pool1)
                DeletedTriangles1.Enqueue(i);
            else
                DeletedTriangles2.Enqueue(i);
        }

        public void RemoveTrianglePatchNeighbours(int index)
        {
            var t = Triangles[index];
            t.IsDeleted = true;
            SetNeighbour(t.T1, t.Edge1.A, t.Edge1.B, -1);
            SetNeighbour(t.T2, t.Edge2.A, t.Edge2.B, -1);
            SetNeighbour(t.T3, t.Edge3.A, t.Edge3.B, -1);
            Triangles[index] = t;
        }

        public float3 F3(int i, float y = 0) => Geometry.V3(Points[i].Position, y);

        public float2 F2(ushort pi1) => Points[pi1].Position;

        private void SetNeighbour(int triangleIndex, ushort vertexA, ushort vertexB, int newNeighbourIndex)
        {
            if (triangleIndex == -1)
                return;
            var t = Triangles[triangleIndex];
            if (t.Edge1.A == vertexB && t.Edge1.B == vertexA)
                t.T1 = newNeighbourIndex;
            else if (t.Edge2.A == vertexB && t.Edge2.B == vertexA)
                t.T2 = newNeighbourIndex;
            else if (t.Edge3.A == vertexB && t.Edge3.B == vertexA)
                t.T3 = newNeighbourIndex;
            else
                Assert.IsTrue(false);
            Triangles[triangleIndex] = t;
        }

        // public bool GetNextTriangleAround(int vertex, ref int cur, ref int prev)
        // {
        //     var t = Triangles[cur];
        //     return CheckNeighbour(vertex, ref cur, ref prev, t.T1) ||
        //            CheckNeighbour(vertex, ref cur, ref prev, t.T2) ||
        //            CheckNeighbour(vertex, ref cur, ref prev, t.T3);
        // }

        // private bool CheckNeighbour(int i, ref int cur1, ref int prev1, int next1)
        // {
        //     if (next1 != -1)
        //     {
        //         var neighbour = Triangles[next1];
        //         if (neighbour.ContainsVertex(i) && next1 != prev1)
        //         {
        //             prev1 = cur1;
        //             cur1 = next1;
        //             return true;
        //         }
        //     }
        //
        //     return false;
        // }

        private bool pool1;

        public void SwapPool()
        {
            pool1 = !pool1;
        }

        // public int FindTriangle(QuadTree quadTree, float2 pos, float quadTreeQueryRadius)
        // {
        //     return quadTree.FindTriangles(new AABB2D(pos, quadTreeQueryRadius), ref this);
        // }
        //
        // public int FindRandomTriangle(ref int haltonSeed)
        // {
        //     int rndTri;
        //     do
        //     {
        //         rndTri = ((int) (HaltonSequence.Halton(++haltonSeed, 3) * Triangles.Length)) %
        //                  Triangles.Length;
        //     } while (Triangles[rndTri].IsDeleted);
        //
        //     return rndTri;
        // }
    }

    public readonly struct Edge : IEquatable<Edge>
    {
        public readonly ushort A;
        public readonly ushort B;

        public Edge(ushort a, ushort b)
        {
            A = a;
            B = b;
        }

        public bool Equals(Edge other) => A.Equals(other.A) && B.Equals(other.B) || A.Equals(other.B) && B.Equals(other.A);

        public override bool Equals(object obj) => obj is Edge other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (A.GetHashCode() * 397) ^ B.GetHashCode();
            }
        }

        public static bool operator ==(Edge left, Edge right) => left.Equals(right);

        public static bool operator !=(Edge left, Edge right) => !left.Equals(right);

        public override string ToString() => $"{nameof(A)}: {A}, {nameof(B)}: {B}";
    }
}