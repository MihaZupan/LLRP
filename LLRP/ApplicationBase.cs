using System.Diagnostics.CodeAnalysis;
using LLRP.Helpers;
using Microsoft.AspNetCore.Server.Kestrel.Core.Internal.Http;

namespace LLRP
{
    internal abstract class ApplicationBase<TApplication> : IHttpConnection
    {
        private static int s_exceptionCounter;

        private const int CRLF = 2;
        private const int ChunkedEncodingMaxChunkLengthDigits = 4; // Valid as long as ResponseContentBufferLength <= 65536
        private const int ChunkedEncodingFinalChunkLength = 1 + CRLF + CRLF;
        protected const int ChunkedEncodingMaxChunkOverhead = ChunkedEncodingMaxChunkLengthDigits + CRLF + CRLF;
        protected const int ChunkedEncodingMaxOverhead = ChunkedEncodingMaxChunkOverhead + ChunkedEncodingFinalChunkLength;

        public PipeReader Reader { get; set; } = null!;
        public PipeWriter Writer { get; set; } = null!;

        private const int ResponseContentBufferLength = 4096;
        protected readonly byte[] ResponseContentBuffer;
        protected readonly Memory<byte> ResponseContentBufferMemory;
        protected readonly Memory<byte> ChunkedResponseContentBuffer;

        protected readonly DownstreamAddress Downstream;

        public abstract Task InitializeAsync();
        public abstract Task ProcessRequestAsync();
        public abstract void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine);
        public abstract void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value);

        public void OnStaticIndexedHeader(int index) { }
        public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value) { }
        public void OnHeadersComplete(bool endStream) { }

        protected ApplicationBase()
        {
            ResponseContentBuffer = new byte[ResponseContentBufferLength];
            ResponseContentBufferMemory = ResponseContentBuffer;
            ChunkedResponseContentBuffer = ResponseContentBuffer.AsMemory(0, ResponseContentBufferLength - ChunkedEncodingMaxOverhead);
            Downstream = DownstreamAddress.GetNextAddress();
        }

        private State _state;
        private readonly HttpParser<ParsingAdapter> _parser = new();

        public async Task ExecuteAsync()
        {
            try
            {
                await InitializeAsync();

                await ProcessRequestsAsync();

                Reader.Complete();
            }
            catch (Exception ex)
            {
                Reader.Complete(ex);

                if (Interlocked.Increment(ref s_exceptionCounter) <= 10)
                {
                    Console.WriteLine(ex);
                }
            }
            finally
            {
                Writer.Complete();
            }
        }

        private async Task ProcessRequestsAsync()
        {
            while (true)
            {
                var readResult = await Reader.ReadAsync();
                var buffer = readResult.Buffer;
                var isCompleted = readResult.IsCompleted;

                if (buffer.IsEmpty && isCompleted)
                {
                    return;
                }

                while (true)
                {
                    ParseHttpRequest(ref buffer, isCompleted);

                    if (_state == State.Body)
                    {
                        await ProcessRequestAsync();

                        _state = State.StartLine;

                        if (!buffer.IsEmpty)
                        {
                            // More input data to parse
                            continue;
                        }
                    }

                    // No more input or incomplete data, Advance the Reader
                    Reader.AdvanceTo(buffer.Start, buffer.End);
                    break;
                }

                await Writer.FlushAsync();
            }
        }

        private void ParseHttpRequest(ref ReadOnlySequence<byte> buffer, bool isCompleted)
        {
            var reader = new SequenceReader<byte>(buffer);
            var state = _state;

            if (state == State.StartLine)
            {
                if (_parser.ParseRequestLine(new ParsingAdapter(this), ref reader))
                {
                    state = State.Headers;
                }
            }

            if (state == State.Headers)
            {
                var success = _parser.ParseHeaders(new ParsingAdapter(this), ref reader);

                if (success)
                {
                    state = State.Body;
                }
            }

            if (state != State.Body && isCompleted)
            {
                ThrowUnexpectedEndOfData();
            }

            _state = state;

            if (state == State.Body)
            {
                // Complete request read, consumed and examined are the same (length 0)
                buffer = buffer.Slice(reader.Position, 0);
            }
            else
            {
                // In-complete request read, consumed is current position and examined is the remaining.
                buffer = buffer.Slice(reader.Position);
            }
        }

        [DoesNotReturn]
        private static void ThrowUnexpectedEndOfData()
        {
            throw new InvalidOperationException("Unexpected end of data!");
        }

        private enum State
        {
            StartLine,
            Headers,
            Body
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected void WriteToWriter(ReadOnlySpan<byte> buffer)
        {
            Span<byte> destination = Writer.GetSpan(buffer.Length);
            Unsafe.CopyBlockUnaligned(ref MemoryMarshal.GetReference(destination), ref MemoryMarshal.GetReference(buffer), (uint)buffer.Length);
            Writer.Advance(buffer.Length);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static BufferWriter<WriterAdapter> GetWriter(PipeWriter pipeWriter, int sizeHint)
            => new(new WriterAdapter(pipeWriter), sizeHint);

        internal struct WriterAdapter : IBufferWriter<byte>
        {
            public PipeWriter Writer;

            public WriterAdapter(PipeWriter writer)
                => Writer = writer;

            public void Advance(int count)
                => Writer.Advance(count);

            public Memory<byte> GetMemory(int sizeHint = 0)
                => Writer.GetMemory(sizeHint);

            public Span<byte> GetSpan(int sizeHint = 0)
                => Writer.GetSpan(sizeHint);
        }

        private struct ParsingAdapter : IHttpRequestLineHandler, IHttpHeadersHandler
        {
            public ApplicationBase<TApplication> RequestHandler;

            public ParsingAdapter(ApplicationBase<TApplication> requestHandler)
                => RequestHandler = requestHandler;

            public void OnStaticIndexedHeader(int index) { }

            public void OnStaticIndexedHeader(int index, ReadOnlySpan<byte> value) { }

            public void OnHeader(ReadOnlySpan<byte> name, ReadOnlySpan<byte> value)
                => RequestHandler.OnHeader(name, value);

            public void OnHeadersComplete(bool endStream) { }

            public void OnStartLine(HttpVersionAndMethod versionAndMethod, TargetOffsetPathLength targetPath, Span<byte> startLine)
                => RequestHandler.OnStartLine(versionAndMethod, targetPath, startLine);
        }
    }
}
