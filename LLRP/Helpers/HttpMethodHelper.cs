using Microsoft.AspNetCore.Http;
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

        public static SystemMethod GetHttpMethod(string method) => method switch
        {
            string mth when HttpMethods.IsGet(mth) => SystemMethod.Get,
            string mth when HttpMethods.IsPost(mth) => SystemMethod.Post,
            string mth when HttpMethods.IsPut(mth) => SystemMethod.Put,
            string mth when HttpMethods.IsDelete(mth) => SystemMethod.Delete,
            string mth when HttpMethods.IsOptions(mth) => SystemMethod.Options,
            string mth when HttpMethods.IsHead(mth) => SystemMethod.Head,
            string mth when HttpMethods.IsPatch(mth) => SystemMethod.Patch,
            string mth when HttpMethods.IsTrace(mth) => SystemMethod.Trace,
            // NOTE: Proxying "CONNECT" is not supported (by design!)
            string mth when HttpMethods.IsConnect(mth) => throw new NotSupportedException($"Unsupported request method '{method}'."),
            _ => new SystemMethod(method)
        };

        public static SystemMethod GetMethod(ReadOnlySpan<byte> method)
        {
            return new SystemMethod(Encoding.ASCII.GetString(method));
        }
    }
}