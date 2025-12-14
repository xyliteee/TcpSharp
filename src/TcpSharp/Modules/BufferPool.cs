namespace TcpSharp.Modules
{
    //Reference implementation of System.Buffer.
    internal static class BufferPool
    {
        public const int POOL_SIZE = 64 * 1024;
        [ThreadStatic] private static byte[] t_buffer;

        public static byte[] Rent()
        {
            byte[] buf = t_buffer;
            buf ??= t_buffer = new byte[POOL_SIZE];
            return buf;
        }
        public static void Return(byte[] buffer) 
        {
            //No-op, compatible with ArrayPool semantics.
        }
    }
}
