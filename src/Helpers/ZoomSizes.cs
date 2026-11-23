namespace DotNetFM;

public static class ZoomSizes
{
    public static readonly int[] Sizes = { 32, 48, 64, 96, 128, 192, 256, 384, 512 };

    public static int NearestIndex(int size)
    {
        int best = 0;
        for (int i = 1; i < Sizes.Length; i++)
        {
            if (Math.Abs(Sizes[i] - size) < Math.Abs(Sizes[best] - size))
                best = i;
        }
        return best;
    }
}
