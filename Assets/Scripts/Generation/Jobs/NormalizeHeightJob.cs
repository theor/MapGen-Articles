using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace Generation.Jobs
{
    [BurstCompile]
    internal struct NormalizeHeightJob : IJobParallelFor
    {
        public float Min;
        public float Max;
        public NativeArray<TriangleData> Input;

        public void Execute(int index)
        {
            var triangleData = Input[index];
            triangleData.Height = math.remap(Min, Max, -1, 1, triangleData.Height);
            Input[index] = triangleData;
        }
    }
}