using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace Generation
{
    public class Generation
    {
        public static NativeArray<float2> GenerateRandomPoints(int count, int size, int seed, Allocator allocator)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var result = new NativeArray<float2>(count, allocator, NativeArrayOptions.UninitializedMemory);
            var fsize = (float2) size;
            new GeneratePointsHaltonJob()
            {
                Points = result,
                Size = fsize, 
                Seed = seed,
            }.Run(count);

            LogSw(sw, "Generate Random Points");
            return result;
        }
        
        [BurstCompile]
        struct GeneratePointsJob : IJob
        {
            public NativeArray<float2> Points;
            public float2 Size;
            public int Seed;
            public void Execute()
            {
                var rnd = new Random((uint) (Seed));

                for (int index = 0; index < Points.Length; index++)
                {
                    Points[index] =
                        rnd.NextFloat2(float2.zero, Size);
                }
            }
        }
        
        [BurstCompile]
        struct GeneratePointsHaltonJob : IJobParallelFor
        {
            public NativeArray<float2> Points;
            public float2 Size;
            public int Seed;
            public void Execute(int index)
            {
                var rnd = new Random((uint) (Seed + index)).NextInt(1, Int32.MaxValue);
                Points[index] = new float2(
                (float) (HaltonSequence.Halton(rnd+index, 7) * Size.x),
                (float) (HaltonSequence.Halton(rnd+index, 11) * Size.y));
            }
        }

        public static void LogSw(Stopwatch sw, string name)
        {
            Debug.Log($"{name}: {sw.Elapsed.TotalMilliseconds}ms");
        }
        
        
    
        #region markers
        static readonly ProfilerMarker s_Marker = new ProfilerMarker("BW-BowyerWatson");
        static readonly ProfilerMarker s_Marker2 = new ProfilerMarker("BW-BowyerWatson Invalid Triangles");
        static readonly ProfilerMarker s_Marker3 = new ProfilerMarker("BW-BowyerWatson Find Hole Boundary");
        static readonly ProfilerMarker s_Marker4 = new ProfilerMarker("BW-BowyerWatson Retriangulate");
        static readonly ProfilerMarker s_Marker5 = new ProfilerMarker("BW-BowyerWatson Setup");
        static readonly ProfilerMarker s_Marker6 = new ProfilerMarker("BW-BowyerWatson Clear");
        static readonly ProfilerMarker s_Marker7 = new ProfilerMarker("BW-BowyerWatson Add To List");
        static readonly ProfilerMarker s_Marker8 = new ProfilerMarker("BW-BowyerWatson Keys");
        #endregion

        public static TriangleStorage DelaunayTriangulation(NativeArray<float2> points, float size)
        {
            var storage = new TriangleStorage(points.Length + 3, Allocator.Persistent);
            var sw = Stopwatch.StartNew();
            var job = new BowyerWatsonJob()
            {
                Size = size,
                Storage = storage,
                Points = points,

                BowyerWatsonMarker = s_Marker,
                InvalidTrianglesMarker = s_Marker2,
                FindHoleBoundariesMarker = s_Marker3,
                RetriangulateMarker = s_Marker4,
                BadTrianglesClearMarker = s_Marker5,
                M6 = s_Marker6,
                AddToBadTrianglesListMarker = s_Marker7,
                M8 = s_Marker8,
            };
            job.Run();

            Generation.LogSw(sw, "Triangulation");
            return storage;
        }
    }
}