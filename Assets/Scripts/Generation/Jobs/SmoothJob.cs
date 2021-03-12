using Unity.Collections;
using Unity.Jobs;

namespace Generation.Jobs
{
    struct SmoothJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<TriangleData> Input;
        [WriteOnly]
        public NativeArray<TriangleData> Output;
        [ReadOnly]
        public TriangleStorage Storage;
        public void Execute(int index)
        {
            
            var t = Storage.Triangles[index];
            var avg = (t.T1 == -1 ? 0 : Input[t.T1].Height) + (t.T2 == -1 ? 0 : Input[t.T2].Height) + (t.T3 == -1 ? 0 : Input[t.T3].Height);
            TriangleData triangleData = Input[index];
            triangleData.Height = avg / 3.0f;

            Output[index] = triangleData;
        }
    }
}