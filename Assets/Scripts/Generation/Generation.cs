using System;
using System.Diagnostics;
using Generation.Jobs;
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
    public static class Generation
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

        public static NativeArray<TriangleData> GenerateHeightMap(in TriangleStorage storage, Params generationParams)
        {
            // DisposeIfCreated(_data);
            var sw = Stopwatch.StartNew();
            // _data = new NativeArray<VertexData>(_s.Points.Length, Allocator.Persistent,NativeArrayOptions.UninitializedMemory);
            var tridata = new NativeArray<TriangleData>(storage.Triangles.Length, Allocator.Persistent,
                NativeArrayOptions.UninitializedMemory);
            var mapJob = new HeightmapJob()
            {
                // _data = _data,
                Tridata = tridata,
                S = storage,
                Params = generationParams,
            };
            mapJob.Run();
            // _data = mapJob._data;
            tridata = mapJob.Tridata;
            LogSw(sw, "HeightMap");
            return tridata;
        }
        
        public static void SmoothHeights(ref NativeArray<TriangleData> tridata, TriangleStorage storage)
        {
            var output = new NativeArray<TriangleData>(tridata, Allocator.Persistent);
            var smoothJob = new SmoothJob
            {
                Input = tridata,
                Output = output,
                Storage = storage,
            };
            var smoothJobHandle = smoothJob.Schedule(tridata.Length, 64);

            tridata.Dispose(smoothJobHandle).Complete();
            tridata = output;
        }

        public static void NormalizeHeights(in NativeArray<TriangleData> tridata, out float minHeight, out float maxHeight)
        {
            var sw = Stopwatch.StartNew();
            var minMaxJob = new MinMaxJob
            {
                Input = tridata,
                Min = new NativeArray<float>(new[] {float.MaxValue}, Allocator.TempJob),
                Max = new NativeArray<float>(new[] {float.MinValue}, Allocator.TempJob),
            };
            minMaxJob.Run(tridata.Length);
            minHeight = minMaxJob.Min[0];
            maxHeight = minMaxJob.Max[0];
            minMaxJob.Min.Dispose();
            minMaxJob.Max.Dispose();
            LogSw(sw, "Find Min/Max Heights");
            sw.Restart();
            var normalizeHeightJob = new NormalizeHeightJob
            {
                Min = minHeight,
                Max = maxHeight,
                Input = tridata,
            };
            normalizeHeightJob.Schedule(tridata.Length, 64).Complete();
            LogSw(sw, "Normalize Heights");
        }
    }

    [Serializable]
    public struct Params
    {
        public int Seed;
        // noise params
        public float NoiseScale;
        public float Lacunarity;
        public int Octaves;
        public float Persistence;

        public float HeightFactor;
        // [Range(-1, 1)] public float SeaLevel;
        //
        // [Range(0, 10)] public float SlopeXOverYFactor;
        //
        // public float SlopeAngleFactor;
        // public float SlopeMinX;
    }
    
    public struct TriangleData
    {
        public float Height;
        public float2 Centroid;
        // public float2 TriSlope;
        // public int LowestNeighbour;
        // public int WaterFlow;
    }
}