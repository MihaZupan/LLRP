using System.Net.Http;

namespace LLRP
{
    internal static class HttpClientConfiguration
    {
        public static bool ShareClients = false;
        public static bool RoundRobin = false;
        public static HttpMessageInvoker[] SharedClients = null!;
        private static int _count;

        public static HttpMessageInvoker CreateClient()
        {
            return new HttpMessageInvoker(new SocketsHttpHandler()
            {
                UseProxy = false,
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.None,
                UseCookies = false,
                ActivityHeadersPropagator = null,
                PooledConnectionIdleTimeout = TimeSpan.FromDays(1), // Avoid the cleaning timer executing during the benchmark
            });
        }

        public static HttpMessageInvoker? GetFixedClient()
        {
            if (ShareClients)
            {
                if (RoundRobin)
                {
                    return null;
                }
                else
                {
                    return SharedClients[Interlocked.Increment(ref _count) % SharedClients.Length];
                }
            }
            else
            {
                return CreateClient();
            }
        }

        public static HttpMessageInvoker GetDynamicClient(ref int countRef)
        {
            HttpMessageInvoker[] clients = SharedClients;
            int count = countRef;

            if ((uint)count < (uint)clients.Length)
            {
                countRef = count + 1;
                return clients[count];
            }
            else
            {
                countRef = 1;
                return clients[0];
            }
        }
    }
}
