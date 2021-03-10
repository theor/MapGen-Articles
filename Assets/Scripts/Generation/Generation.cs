using System;
using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Random = Unity.Mathematics.Random;

namespace Generation
{
    public class Generation
    {
        public static NativeArray<float2> GenerateRandomPoints(int count, Vector2Int size, int seed)
        {
            Stopwatch sw = Stopwatch.StartNew();
            var result = new NativeArray<float2>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var fsize = (float2) (Vector2) size;
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

        static void LogSw(Stopwatch sw, string name)
        {
            Debug.Log($"{name}: {sw.Elapsed.TotalMilliseconds}ms");
        }
    }
}