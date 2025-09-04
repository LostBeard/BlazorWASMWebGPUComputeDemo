namespace BlazorWASMWebGPUComputeDemo
{
    public static class NumberArrayExtensions
    {
        public static long ByteLength(this byte[] bytes) => bytes.Length;
        public static long ByteLength(this ushort[] data) => data.Length * 2;
        public static long ByteLength(this uint[] data) => data.Length * 4;
        public static long ByteLength(this ulong[] data) => data.Length * 8;
        public static long ByteLength(this sbyte[] bytes) => bytes.Length;
        public static long ByteLength(this short[] data) => data.Length * 2;
        public static long ByteLength(this int[] data) => data.Length * 4;
        public static long ByteLength(this long[] data) => data.Length * 8;
        public static long ByteLength(this Half[] data) => data.Length * 2;
        public static long ByteLength(this float[] data) => data.Length * 4;
        public static long ByteLength(this double[] data) => data.Length * 8;
    }
}
