using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace LLRP
{
    internal sealed partial class HttpClientApplication : ApplicationBase<HttpClientApplication>
    {
        public override Task InitializeAsync()
        {
            throw new NotImplementedException();
        }

        public override Task ProcessRequestAsync()
        {
            throw new NotImplementedException();
        }

        public override void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
        {
            throw new NotImplementedException();
        }

        public override void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
        {
            throw new NotImplementedException();
        }
    }
}
