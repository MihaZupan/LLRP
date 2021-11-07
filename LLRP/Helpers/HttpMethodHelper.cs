using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;
using SystemMethod = System.Net.Http.HttpMethod;

namespace LLRP.Helpers
{
    internal static class HttpMethodHelper
    {
        public static SystemMethod? TryConvertMethod(HttpMethod httpMethod)
        {
            return httpMethod switch
            {
                HttpMethod.Get => SystemMethod.Get,
                HttpMethod.Put => SystemMethod.Put,
                HttpMethod.Delete => SystemMethod.Delete,
                HttpMethod.Post => SystemMethod.Post,
                HttpMethod.Head => SystemMethod.Head,
                HttpMethod.Trace => SystemMethod.Trace,
                HttpMethod.Patch => SystemMethod.Patch,
                HttpMethod.Options => SystemMethod.Options,
                _ => null
            };
        }

        public static SystemMethod GetMethod(ReadOnlySpan<byte> method)
        {
            return new SystemMethod(Encoding.ASCII.GetString(method));
        }
    }
}