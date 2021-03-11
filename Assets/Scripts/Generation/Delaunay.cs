using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine.Assertions;

namespace Generation
{
    [BurstCompile]
    struct BowyerWatsonJob : IJob
    {
        public NativeArray<float2> Points;
        public float Size;
        public TriangleStorage Storage;

        public ProfilerMarker BowyerWatsonMarker,
            InvalidTrianglesMarker,
            FindHoleBoundariesMarker,
            RetriangulateMarker,
            BadTrianglesClearMarker,
            M6,
            AddToBadTrianglesListMarker,
            M8;

        public void Execute()
        {
            BowyerWatson(Size, Points);
        }

        private void BowyerWatson(float size, NativeArray<float2> points)
        {
            BowyerWatsonMarker.Begin();

            Assert.IsFalse(Storage.Points.Length > UInt16.MaxValue - 3, "Last 3 Ids reserved for the super triangle");
            Storage.AddVertices(points);
            var superTriangleA = new float2(0.5f * size, -2.5f * size);
            Storage.AddVertex(points.Length, superTriangleA);
            var superTriangleB = new float2(-1.5f * size, 2.5f * size);
            Storage.AddVertex(points.Length + 1, superTriangleB);
            var superTriangleC = new float2(2.5f * size, 2.5f * size);
            Storage.AddVertex(points.Length + 2, superTriangleC);

            Storage.AddTriangle((ushort) points.Length, (ushort) (points.Length + 1), (ushort) (points.Length + 2));

            // maps a Vertex Index to a vertex (including the special cases of the super triangle)
            var badTriangles = new NativeList<int>(100, Allocator.Temp);
            var polygon = new NativeList<TriangleStorage.EdgeRef>(10, Allocator.Temp);
            NativeList<int> newTriangles = new NativeList<int>(polygon.Length, Allocator.Temp);

            for (int ip = 0; ip < Storage.Points.Length - 3; ip++)
            {
                BadTrianglesClearMarker.Begin();
                badTriangles.Clear();
                BadTrianglesClearMarker.End();

                InvalidTrianglesMarker.Begin();
                var point = Storage.Points[ip];

                // first find all the triangles that are no longer valid due to the insertion
                // slowest part (~93% of the time is spent here)
                var trianglesLength = Storage.Triangles.Length;
                for (int index = 0; index < trianglesLength; index++)
                {
                    var triangle = Storage.Triangles[index];
                    if (triangle.IsDeleted)
                        continue;
                    // if point is inside circumcircle of triangle (cached in the triangle)
                    if (math.distancesq(point.Position, triangle.CircumCircleCenter.xz) <
                        triangle.CircumCircleRadiusSquared)
                    {
                        // found first bad triangle
                        // recurse to find other bad triangles which will all be connected to this one
                        badTriangles.Add(index);
                        Rec(in point, index, ref badTriangles);
                        break;
                    }
                }

                // for (int index = 0; index < trianglesLength; index++)
                // {
                //     var triangle = Storage.Triangles[index];
                //     if (triangle.IsDeleted)
                //         continue;
                //     // if point is inside circumcircle of triangle (cached in the triangle)
                //     if (math.distancesq(point.Position, triangle.CircumCircleCenter.xz) < triangle.CircumCircleRadiusSquared)
                //     {
                //         AddToBadTrianglesListMarker.Begin();
                //         badTriangles.Add(index);
                //         AddToBadTrianglesListMarker.End();
                //     }
                // }

                InvalidTrianglesMarker.End();

                FindHoleBoundariesMarker.Begin();
                polygon.Clear();
                // find the boundary of the polygonal hole
                for (int index = 0; index < badTriangles.Length; index++)
                {
                    int badTriangle = badTriangles[index];
                    AddNonSharedEdgeToPolygon(Storage, badTriangles, badTriangle, 0, ref polygon);
                    AddNonSharedEdgeToPolygon(Storage, badTriangles, badTriangle, 1, ref polygon);
                    AddNonSharedEdgeToPolygon(Storage, badTriangles, badTriangle, 2, ref polygon);
                }

                FindHoleBoundariesMarker.End();

                RetriangulateMarker.Begin();
                // remove them from the data structure
                for (int index = badTriangles.Length - 1; index >= 0; index--)
                {
                    int i = badTriangles[index];
                    Storage.RemoveTriangle(i);
                    // triangulation.RemoveAtSwapBack(i);
                }

                Storage.SwapPool();

                newTriangles.Clear();
                // re-triangulate the polygonal hole
                for (int index = 0; index < polygon.Length; index++)
                {
                    var edge = polygon[index];
                    // potential optim: use deleted triangles as some kind of quadtree (tritree ?)
                    // index tris by their circumcenter
                    // that sets t1. t2,t3 set below
                    var newTriangleIndex =
                        Storage.AddTriangle(edge,
                            (ushort) ip); // TODO !!! need to delay triangle pooling as AddTriangle uses the previous one
                    newTriangles.Add(newTriangleIndex);
                }

                // set t2,t3 as t1 is always the polygon boundary
                for (int i = 0; i < newTriangles.Length; i++)
                {
                    var t1 = Storage.Triangles[newTriangles[i]];
                    for (int j = 0; j < newTriangles.Length; j++)
                    {
                        if (i == j)
                            continue;
                        var t2 = Storage.Triangles[newTriangles[j]];
                        if (t1.Edge1.B == t2.Edge1.A)
                        {
                            t1.T2 = newTriangles[j];
                            t2.T3 = newTriangles[i];
                            Storage.Triangles[newTriangles[i]] = t1;
                            Storage.Triangles[newTriangles[j]] = t2;
                        }
                    }
                }

                // Storage.Check();

                RetriangulateMarker.End();
            }

            badTriangles.Dispose();

            newTriangles.Dispose();
            polygon.Dispose();

            // cleanup
            for (int index = 0; index < Storage.Triangles.Length; index++)
            {
                var triangle = Storage.Triangles[index];
                // if triangle contains a vertex from original super-triangle
                if (!triangle.IsDeleted && (triangle.ContainsVertex(points.Length) ||
                                            triangle.ContainsVertex(points.Length + 1) ||
                                            triangle.ContainsVertex(points.Length + 2)))
                    Storage.RemoveTrianglePatchNeighbours(index);
            }

            BowyerWatsonMarker.End();
        }

        private void Rec(in TriangleStorage.Vertex v, int triangleIndex, ref NativeList<int> badTriangles)
        {
            var triangle = Storage.Triangles[triangleIndex];
            CheckNeighbour(in v, triangle.T1, ref badTriangles);
            CheckNeighbour(in v, triangle.T2, ref badTriangles);
            CheckNeighbour(in v, triangle.T3, ref badTriangles);
        }

        private void CheckNeighbour(in TriangleStorage.Vertex v, int triangleIndex, ref NativeList<int> badTriangles)
        {
            if (triangleIndex == -1)
                return;
            var n1 = Storage.Triangles[triangleIndex];
            var badTrianglesLength = badTriangles.Length;
            for (int i = 0; i < badTrianglesLength; i++)
            {
                if (badTriangles[i] == triangleIndex) // already added
                    return;
            }

            if (math.distancesq(v.Position, n1.CircumCircleCenter.xz) < n1.CircumCircleRadiusSquared)
            {
                badTriangles.Add(triangleIndex);
                Rec(v, triangleIndex, ref badTriangles);
            }
        }

        private static void AddNonSharedEdgeToPolygon(TriangleStorage storage, NativeList<int> badTriangleIndices,
            int badTriangleIndex, int edgeIndex,
            ref NativeList<TriangleStorage.EdgeRef> polygon)
        {
            var badTriangle = storage.Triangles[badTriangleIndex];
            var edge = edgeIndex == 0 ? badTriangle.Edge1 : edgeIndex == 1 ? badTriangle.Edge2 : badTriangle.Edge3;
            // if edge is not shared by any other triangles in badTriangles
            bool any = false;
            for (var index = 0; index < badTriangleIndices.Length; index++)
            {
                var t = badTriangleIndices[index];
                if (t != badTriangleIndex)
                {
                    var otherBadTriangle = storage.Triangles[t];
                    if (otherBadTriangle.Edge1 == edge || otherBadTriangle.Edge2 == edge ||
                        otherBadTriangle.Edge3 == edge)
                    {
                        any = true;
                        break;
                    }
                }
            }

            if (!any)
                polygon.Add(new TriangleStorage.EdgeRef(badTriangleIndex, edgeIndex));
        }
    }
}