using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace Generation.Jobs
{
    [BurstCompile]
    struct MinMaxJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<TriangleData> Input;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<float> Min;

        [NativeDisableParallelForRestriction]
        public NativeArray<float> Max;

        public unsafe void Execute(int index)
        {
            var height = Input[index].Height;
            float min;
            ref var minPtr = ref UnsafeUtility.AsRef<float>((float*) Min.GetUnsafePtr());
            ref var maxPtr = ref UnsafeUtility.AsRef<float>((float*) Max.GetUnsafePtr());
            do
            {
                min = Min[0];
                if (height > min)
                    break;
                
            } while (min != Interlocked.CompareExchange(ref minPtr, height, min));
            float max;
            do
            {
                max = Max[0];
                if (height < max)
                    break;
            } while (max != Interlocked.CompareExchange(ref maxPtr, height, max));
        }
    }
}