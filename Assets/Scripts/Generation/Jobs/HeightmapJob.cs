using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Generation.Jobs
{
    [BurstCompile]
    struct HeightmapJob : IJob
    {
        public NativeArray<TriangleData> Tridata;
        public TriangleStorage S;
        public Params Params;

        public unsafe void Execute()
        {
            var triData = ((TriangleData*) Tridata.GetUnsafePtr());
            float2 seedOffset;
            unchecked
            {
                seedOffset = new float2(
                    (float)(HaltonSequence.Halton(Params.Seed << 7, 7)),
                    (float)(HaltonSequence.Halton(Params.Seed << 11, 11)));
            }
            for (var index = 0; index < S.Triangles.Length; index++)
            {
                var t = S.Triangles[index];

                // average height per triangle
                var a = S.F2(t.V1);
                var b = S.F2(t.V2);
                var c = S.F2(t.V3);
                var p = (a + b + c) / 3.0f;
                (triData + index)->Centroid = p;
                p = (p + seedOffset) / Params.NoiseScale;

                var height =
                        fbm(p + fbm(p))
                    ;
                (triData + index)->Height = height;
                // (triData + index)->WaterFlow = 0;
            }
        }

        private float fbm(float2 pos)
        {
            float g = math.exp2(-Params.Persistence);
            float f = 1.0f;
            float a = 1.0f;
            float t = 0.0f;
            for (int i = 0; i < Params.Octaves; i++)
            {
                t += a * noise.snoise(f * pos);
                f *= Params.Lacunarity;
                a *= g;
            }

            return t;
        }
    }
}