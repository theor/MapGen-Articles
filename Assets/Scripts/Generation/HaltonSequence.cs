using UnityEngine;

namespace Generation
{
    public static class HaltonSequence
    {
        public static int HaltonInt(int index, int nbase, int max)
        {
            return (int) (Halton(index, nbase) * max);
        }
        public static double Halton(int index, int nbase)
        {
            double fraction = 1;
            double result = 0;
            while (index > 0)
            {
                fraction /= nbase;
                result += fraction * (index % nbase);
                index = ~~(index / nbase); // floor division
            }

            return result;
        }
        
        public static Color ColorFromIndex(int index, int hbase = 3, float v = 0.5f)
        {
            return Color.HSVToRGB((float) HaltonSequence.Halton(index, hbase), 1, v);
        }
    }
}