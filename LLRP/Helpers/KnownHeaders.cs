using System.Linq;

namespace LLRP.Helpers
{
    public class KnownHeaders
    {
        public sealed class KnownHeader
        {
            public KnownHeader(string name, string[]? knownValues = null)
            {
                Name = name;
                KnownValues = knownValues;
                NameBytes = Encoding.ASCII.GetBytes(name);

                if (knownValues is not null)
                {
                    KnownValueBytes = knownValues.Select(v => Encoding.UTF8.GetBytes(v)).ToArray();
                }
            }

            public readonly string Name;
            public readonly byte[] NameBytes;
            public readonly string[]? KnownValues;
            public readonly byte[][]? KnownValueBytes;

            public string? TryGetValue(ReadOnlySpan<byte> value)
            {
                var values = KnownValueBytes;
                if (values is not null)
                {
                    for (int i = 0; i < values.Length; i++)
                    {
                        if (value.SequenceEqual(values[i]))
                        {
                            return KnownValues![i];
                        }
                    }
                }

                return null;
            }
        }

        public static readonly KnownHeader PseudoStatus = new KnownHeader(":status");
        public static readonly KnownHeader Accept = new KnownHeader("Accept");
        public static readonly KnownHeader AcceptCharset = new KnownHeader("Accept-Charset");
        public static readonly KnownHeader AcceptEncoding = new KnownHeader("Accept-Encoding");
        public static readonly KnownHeader AcceptLanguage = new KnownHeader("Accept-Language");
        public static readonly KnownHeader AcceptPatch = new KnownHeader("Accept-Patch");
        public static readonly KnownHeader AcceptRanges = new KnownHeader("Accept-Ranges");
        public static readonly KnownHeader AccessControlAllowCredentials = new KnownHeader("Access-Control-Allow-Credentials", new string[] { "true" });
        public static readonly KnownHeader AccessControlAllowHeaders = new KnownHeader("Access-Control-Allow-Headers", new string[] { "*" });
        public static readonly KnownHeader AccessControlAllowMethods = new KnownHeader("Access-Control-Allow-Methods", new string[] { "*" });
        public static readonly KnownHeader AccessControlAllowOrigin = new KnownHeader("Access-Control-Allow-Origin", new string[] { "*", "null" });
        public static readonly KnownHeader AccessControlExposeHeaders = new KnownHeader("Access-Control-Expose-Headers", new string[] { "*" });
        public static readonly KnownHeader AccessControlMaxAge = new KnownHeader("Access-Control-Max-Age");
        public static readonly KnownHeader Age = new KnownHeader("Age");
        public static readonly KnownHeader Allow = new KnownHeader("Allow");
        public static readonly KnownHeader AltSvc = new KnownHeader("Alt-Svc");
        public static readonly KnownHeader AltUsed = new KnownHeader("Alt-Used");
        public static readonly KnownHeader Authorization = new KnownHeader("Authorization");
        public static readonly KnownHeader CacheControl = new KnownHeader("Cache-Control", new string[] { "must-revalidate", "no-cache", "no-store", "no-transform", "private", "proxy-revalidate", "public" });
        public static readonly KnownHeader Connection = new KnownHeader("Connection", new string[] { "keep-alive", "close" });
        public static readonly KnownHeader ContentDisposition = new KnownHeader("Content-Disposition", new string[] { "inline", "attachment" });
        public static readonly KnownHeader ContentEncoding = new KnownHeader("Content-Encoding", new string[] { "gzip", "deflate", "br", "compress", "identity" });
        public static readonly KnownHeader ContentLanguage = new KnownHeader("Content-Language");
        public static readonly KnownHeader ContentLength = new KnownHeader("Content-Length");
        public static readonly KnownHeader ContentLocation = new KnownHeader("Content-Location");
        public static readonly KnownHeader ContentMD5 = new KnownHeader("Content-MD5");
        public static readonly KnownHeader ContentRange = new KnownHeader("Content-Range");
        public static readonly KnownHeader ContentSecurityPolicy = new KnownHeader("Content-Security-Policy");
        public static readonly KnownHeader ContentType = new KnownHeader("Content-Type");
        public static readonly KnownHeader Cookie = new KnownHeader("Cookie");
        public static readonly KnownHeader Cookie2 = new KnownHeader("Cookie2");
        public static readonly KnownHeader Date = new KnownHeader("Date");
        public static readonly KnownHeader ETag = new KnownHeader("ETag");
        public static readonly KnownHeader Expect = new KnownHeader("Expect", new string[] { "100-continue" });
        public static readonly KnownHeader ExpectCT = new KnownHeader("Expect-CT");
        public static readonly KnownHeader Expires = new KnownHeader("Expires");
        public static readonly KnownHeader From = new KnownHeader("From");
        public static readonly KnownHeader GrpcEncoding = new KnownHeader("grpc-encoding", new string[] { "identity", "gzip", "deflate" });
        public static readonly KnownHeader GrpcMessage = new KnownHeader("grpc-message");
        public static readonly KnownHeader GrpcStatus = new KnownHeader("grpc-status", new string[] { "0" });
        public static readonly KnownHeader Host = new KnownHeader("Host");
        public static readonly KnownHeader IfMatch = new KnownHeader("If-Match");
        public static readonly KnownHeader IfModifiedSince = new KnownHeader("If-Modified-Since");
        public static readonly KnownHeader IfNoneMatch = new KnownHeader("If-None-Match");
        public static readonly KnownHeader IfRange = new KnownHeader("If-Range");
        public static readonly KnownHeader IfUnmodifiedSince = new KnownHeader("If-Unmodified-Since");
        public static readonly KnownHeader KeepAlive = new KnownHeader("Keep-Alive");
        public static readonly KnownHeader LastModified = new KnownHeader("Last-Modified");
        public static readonly KnownHeader Link = new KnownHeader("Link");
        public static readonly KnownHeader Location = new KnownHeader("Location");
        public static readonly KnownHeader MaxForwards = new KnownHeader("Max-Forwards");
        public static readonly KnownHeader Origin = new KnownHeader("Origin");
        public static readonly KnownHeader P3P = new KnownHeader("P3P");
        public static readonly KnownHeader Pragma = new KnownHeader("Pragma", new string[] { "no-cache" });
        public static readonly KnownHeader ProxyAuthenticate = new KnownHeader("Proxy-Authenticate");
        public static readonly KnownHeader ProxyAuthorization = new KnownHeader("Proxy-Authorization");
        public static readonly KnownHeader ProxyConnection = new KnownHeader("Proxy-Connection");
        public static readonly KnownHeader ProxySupport = new KnownHeader("Proxy-Support");
        public static readonly KnownHeader PublicKeyPins = new KnownHeader("Public-Key-Pins");
        public static readonly KnownHeader Range = new KnownHeader("Range");
        public static readonly KnownHeader Referer = new KnownHeader("Referer"); // NB: The spelling-mistake "Referer" for "Referrer" must be matched.
        public static readonly KnownHeader ReferrerPolicy = new KnownHeader("Referrer-Policy", new string[] { "strict-origin-when-cross-origin", "origin-when-cross-origin", "strict-origin", "origin", "same-origin", "no-referrer-when-downgrade", "no-referrer", "unsafe-url" });
        public static readonly KnownHeader Refresh = new KnownHeader("Refresh");
        public static readonly KnownHeader RetryAfter = new KnownHeader("Retry-After");
        public static readonly KnownHeader SecWebSocketAccept = new KnownHeader("Sec-WebSocket-Accept");
        public static readonly KnownHeader SecWebSocketExtensions = new KnownHeader("Sec-WebSocket-Extensions");
        public static readonly KnownHeader SecWebSocketKey = new KnownHeader("Sec-WebSocket-Key");
        public static readonly KnownHeader SecWebSocketProtocol = new KnownHeader("Sec-WebSocket-Protocol");
        public static readonly KnownHeader SecWebSocketVersion = new KnownHeader("Sec-WebSocket-Version");
        public static readonly KnownHeader Server = new KnownHeader("Server");
        public static readonly KnownHeader ServerTiming = new KnownHeader("Server-Timing");
        public static readonly KnownHeader SetCookie = new KnownHeader("Set-Cookie");
        public static readonly KnownHeader SetCookie2 = new KnownHeader("Set-Cookie2");
        public static readonly KnownHeader StrictTransportSecurity = new KnownHeader("Strict-Transport-Security");
        public static readonly KnownHeader TE = new KnownHeader("TE", new string[] { "trailers", "compress", "deflate", "gzip" });
        public static readonly KnownHeader TSV = new KnownHeader("TSV");
        public static readonly KnownHeader Trailer = new KnownHeader("Trailer");
        public static readonly KnownHeader TransferEncoding = new KnownHeader("Transfer-Encoding", new string[] { "chunked", "compress", "deflate", "gzip", "identity" });
        public static readonly KnownHeader Upgrade = new KnownHeader("Upgrade");
        public static readonly KnownHeader UpgradeInsecureRequests = new KnownHeader("Upgrade-Insecure-Requests", new string[] { "1" });
        public static readonly KnownHeader UserAgent = new KnownHeader("User-Agent");
        public static readonly KnownHeader Vary = new KnownHeader("Vary", new string[] { "*" });
        public static readonly KnownHeader Via = new KnownHeader("Via");
        public static readonly KnownHeader WWWAuthenticate = new KnownHeader("WWW-Authenticate");
        public static readonly KnownHeader Warning = new KnownHeader("Warning");
        public static readonly KnownHeader XAspNetVersion = new KnownHeader("X-AspNet-Version");
        public static readonly KnownHeader XCache = new KnownHeader("X-Cache");
        public static readonly KnownHeader XContentDuration = new KnownHeader("X-Content-Duration");
        public static readonly KnownHeader XContentTypeOptions = new KnownHeader("X-Content-Type-Options", new string[] { "nosniff" });
        public static readonly KnownHeader XFrameOptions = new KnownHeader("X-Frame-Options", new string[] { "DENY", "SAMEORIGIN" });
        public static readonly KnownHeader XMSEdgeRef = new KnownHeader("X-MSEdge-Ref");
        public static readonly KnownHeader XPoweredBy = new KnownHeader("X-Powered-By");
        public static readonly KnownHeader XRequestID = new KnownHeader("X-Request-ID");
        public static readonly KnownHeader XUACompatible = new KnownHeader("X-UA-Compatible");
        public static readonly KnownHeader XXssProtection = new KnownHeader("X-XSS-Protection", new string[] { "0", "1", "1; mode=block" });

        internal static unsafe KnownHeader? TryGetKnownHeader(ReadOnlySpan<byte> name)
        {
            KnownHeader? candidate = GetCandidate(ref MemoryMarshal.GetReference(name), name.Length);
            if (candidate != null && name.SequenceEqual(candidate.NameBytes))
            {
                return candidate;
            }

            return null;
        }

        private static KnownHeader? GetCandidate(ref byte key, int length)
        {
            switch (length)
            {
                case 2:
                    return TE; // TE

                case 3:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'A': return Age; // [A]ge
                        case (byte)'P': return P3P; // [P]3P
                        case (byte)'T': return TSV; // [T]SV
                        case (byte)'V': return Via; // [V]ia
                    }
                    break;

                case 4:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'D': return Date; // [D]ate
                        case (byte)'E': return ETag; // [E]Tag
                        case (byte)'F': return From; // [F]rom
                        case (byte)'H': return Host; // [H]ost
                        case (byte)'L': return Link; // [L]ink
                        case (byte)'V': return Vary; // [V]ary
                    }
                    break;

                case 5:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'A': return Allow; // [A]llow
                        case (byte)'R': return Range; // [R]ange
                    }
                    break;

                case 6:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'A': return Accept; // [A]ccept
                        case (byte)'C': return Cookie; // [C]ookie
                        case (byte)'E': return Expect; // [E]xpect
                        case (byte)'O': return Origin; // [O]rigin
                        case (byte)'P': return Pragma; // [P]ragma
                        case (byte)'S': return Server; // [S]erver
                    }
                    break;

                case 7:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)':': return PseudoStatus; // [:]status
                        case (byte)'A': return AltSvc;  // [A]lt-Svc
                        case (byte)'C': return Cookie2; // [C]ookie2
                        case (byte)'E': return Expires; // [E]xpires
                        case (byte)'R':
                            switch (KeyAt(ref key, 3))
                            {
                                case (byte)'e': return Referer; // [R]ef[e]rer
                                case (byte)'r': return Refresh; // [R]ef[r]esh
                            }
                            break;
                        case (byte)'T': return Trailer; // [T]railer
                        case (byte)'U': return Upgrade; // [U]pgrade
                        case (byte)'W': return Warning; // [W]arning
                        case (byte)'X': return XCache;  // [X]-Cache
                    }
                    break;

                case 8:
                    switch (KeyAt(ref key, 3))
                    {
                        case (byte)'-': return AltUsed;  // Alt[-]Used
                        case (byte)'a': return Location; // Loc[a]tion
                        case (byte)'M': return IfMatch;  // If-[M]atch
                        case (byte)'R': return IfRange;  // If-[R]ange
                    }
                    break;

                case 9:
                    return ExpectCT; // Expect-CT

                case 10:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'C': return Connection; // [C]onnection
                        case (byte)'K': return KeepAlive;  // [K]eep-Alive
                        case (byte)'S': return SetCookie;  // [S]et-Cookie
                        case (byte)'U': return UserAgent;  // [U]ser-Agent
                    }
                    break;

                case 11:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'C': return ContentMD5; // [C]ontent-MD5
                        case (byte)'g': return GrpcStatus; // [g]rpc-status
                        case (byte)'R': return RetryAfter; // [R]etry-After
                        case (byte)'S': return SetCookie2; // [S]et-Cookie2
                    }
                    break;

                case 12:
                    switch (KeyAt(ref key, 5))
                    {
                        case (byte)'d': return XMSEdgeRef;  // X-MSE[d]ge-Ref
                        case (byte)'e': return XPoweredBy;  // X-Pow[e]red-By
                        case (byte)'m': return GrpcMessage; // grpc-[m]essage
                        case (byte)'n': return ContentType; // Conte[n]t-Type
                        case (byte)'o': return MaxForwards; // Max-F[o]rwards
                        case (byte)'t': return AcceptPatch; // Accep[t]-Patch
                        case (byte)'u': return XRequestID;  // X-Req[u]est-ID
                    }
                    break;

                case 13:
                    switch (KeyAt(ref key, 12))
                    {
                        case (byte)'L': return LastModified;  // Last-Modifie[d]
                        case (byte)'C': return ContentRange;  // Content-Rang[e]
                        case (byte)'g':
                            switch (KeyAt(ref key, 0))
                            {
                                case (byte)'S': return ServerTiming;  // [S]erver-Timin[g]
                                case (byte)'g': return GrpcEncoding;  // [g]rpc-encodin[g]
                            }
                            break;
                        case (byte)'h': return IfNoneMatch;   // If-None-Matc[h]
                        case (byte)'l': return CacheControl;  // Cache-Contro[l]
                        case (byte)'n': return Authorization; // Authorizatio[n]
                        case (byte)'s': return AcceptRanges;  // Accept-Range[s]
                        case (byte)'t': return ProxySupport;  // Proxy-Suppor[t]
                    }
                    break;

                case 14:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'A': return AcceptCharset; // [A]ccept-Charset
                        case (byte)'C': return ContentLength; // [C]ontent-Length
                    }
                    break;

                case 15:
                    switch (KeyAt(ref key, 7))
                    {
                        case (byte)'-': return XFrameOptions;  // X-Frame[-]Options
                        case (byte)'E': return AcceptEncoding; // Accept-[E]ncoding
                        case (byte)'K': return PublicKeyPins;  // Public-[K]ey-Pins
                        case (byte)'L': return AcceptLanguage; // Accept-[L]anguage
                        case (byte)'m': return XUACompatible;  // X-UA-Co[m]patible
                        case (byte)'r': return ReferrerPolicy; // Referre[r]-Policy
                    }
                    break;

                case 16:
                    switch (KeyAt(ref key, 11))
                    {
                        case (byte)'a': return ContentLocation; // Content-Loc[a]tion
                        case (byte)'c':
                            switch (KeyAt(ref key, 0))
                            {
                                case (byte)'P': return ProxyConnection; // [P]roxy-Conne[c]tion
                                case (byte)'X': return XXssProtection;  // [X]-XSS-Prote[c]tion
                            }
                            break;
                        case (byte)'g': return ContentLanguage; // Content-Lan[g]uage
                        case (byte)'i': return WWWAuthenticate; // WWW-Authent[i]cate
                        case (byte)'o': return ContentEncoding; // Content-Enc[o]ding
                        case (byte)'r': return XAspNetVersion;  // X-AspNet-Ve[r]sion
                    }
                    break;

                case 17:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'I': return IfModifiedSince;  // [I]f-Modified-Since
                        case (byte)'S': return SecWebSocketKey;  // [S]ec-WebSocket-Key
                        case (byte)'T': return TransferEncoding; // [T]ransfer-Encoding
                    }
                    break;

                case 18:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'P': return ProxyAuthenticate; // [P]roxy-Authenticate
                        case (byte)'X': return XContentDuration;  // [X]-Content-Duration
                    }
                    break;

                case 19:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'C': return ContentDisposition; // [C]ontent-Disposition
                        case (byte)'I': return IfUnmodifiedSince;  // [I]f-Unmodified-Since
                        case (byte)'P': return ProxyAuthorization; // [P]roxy-Authorization
                    }
                    break;

                case 20:
                    return SecWebSocketAccept; // Sec-WebSocket-Accept

                case 21:
                    return SecWebSocketVersion; // Sec-WebSocket-Version

                case 22:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'A': return AccessControlMaxAge;  // [A]ccess-Control-Max-Age
                        case (byte)'S': return SecWebSocketProtocol; // [S]ec-WebSocket-Protocol
                        case (byte)'X': return XContentTypeOptions;  // [X]-Content-Type-Options
                    }
                    break;

                case 23:
                    return ContentSecurityPolicy; // Content-Security-Policy

                case 24:
                    return SecWebSocketExtensions; // Sec-WebSocket-Extensions

                case 25:
                    switch (KeyAt(ref key, 0))
                    {
                        case (byte)'S': return StrictTransportSecurity; // [S]trict-Transport-Security
                        case (byte)'U': return UpgradeInsecureRequests; // [U]pgrade-Insecure-Requests
                    }
                    break;

                case 27:
                    return AccessControlAllowOrigin; // Access-Control-Allow-Origin

                case 28:
                    switch (KeyAt(ref key, 21))
                    {
                        case (byte)'H': return AccessControlAllowHeaders; // Access-Control-Allow-[H]eaders
                        case (byte)'M': return AccessControlAllowMethods; // Access-Control-Allow-[M]ethods
                    }
                    break;

                case 29:
                    return AccessControlExposeHeaders; // Access-Control-Expose-Headers

                case 32:
                    return AccessControlAllowCredentials; // Access-Control-Allow-Credentials
            }

            return null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static byte KeyAt(ref byte key, nint offset) => Unsafe.AddByteOffset(ref key, offset);
        }
    }
}