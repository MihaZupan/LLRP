using System.Linq;

namespace LLRP.Helpers
{
    public class KnownHeaders
    {
        public sealed class KnownHeader
        {
            private static readonly Encoding _utf8 = Encoding.UTF8;

            public KnownHeader(string name, string[]? knownValues = null)
            {
                Name = name;
                KnownValues = knownValues;
                NameBytes = Encoding.ASCII.GetBytes(name);

                if (knownValues is not null)
                {
                    KnownValueBytes = knownValues.Select(v => _utf8.GetBytes(v)).ToArray();
                }
            }

            public readonly string Name;
            public readonly byte[] NameBytes;
            public readonly string[]? KnownValues;
            public readonly byte[][]? KnownValueBytes;

            public string GetValue(ReadOnlySpan<byte> value)
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

                return _utf8.GetString(value);
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
        public static readonly KnownHeader Connection = new KnownHeader("Connection", new string[] { "close" });
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
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'a': return Age; // [A]ge
                        case 'p': return P3P; // [P]3P
                        case 't': return TSV; // [T]SV
                        case 'v': return Via; // [V]ia
                    }
                    break;

                case 4:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'd': return Date; // [D]ate
                        case 'e': return ETag; // [E]Tag
                        case 'f': return From; // [F]rom
                        case 'h': return Host; // [H]ost
                        case 'l': return Link; // [L]ink
                        case 'v': return Vary; // [V]ary
                    }
                    break;

                case 5:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'a': return Allow; // [A]llow
                        case 'r': return Range; // [R]ange
                    }
                    break;

                case 6:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'a': return Accept; // [A]ccept
                        case 'c': return Cookie; // [C]ookie
                        case 'e': return Expect; // [E]xpect
                        case 'o': return Origin; // [O]rigin
                        case 'p': return Pragma; // [P]ragma
                        case 's': return Server; // [S]erver
                    }
                    break;

                case 7:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case ':': return PseudoStatus; // [:]status
                        case 'a': return AltSvc;  // [A]lt-Svc
                        case 'c': return Cookie2; // [C]ookie2
                        case 'e': return Expires; // [E]xpires
                        case 'r':
                            switch (KeyAt(ref key, 3) | 0x20)
                            {
                                case 'e': return Referer; // [R]ef[e]rer
                                case 'r': return Refresh; // [R]ef[r]esh
                            }
                            break;
                        case 't': return Trailer; // [T]railer
                        case 'u': return Upgrade; // [U]pgrade
                        case 'w': return Warning; // [W]arning
                        case 'x': return XCache;  // [X]-Cache
                    }
                    break;

                case 8:
                    switch (KeyAt(ref key, 3) | 0x20)
                    {
                        case '-': return AltUsed;  // Alt[-]Used
                        case 'a': return Location; // Loc[a]tion
                        case 'm': return IfMatch;  // If-[M]atch
                        case 'r': return IfRange;  // If-[R]ange
                    }
                    break;

                case 9:
                    return ExpectCT; // Expect-CT

                case 10:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'c': return Connection; // [C]onnection
                        case 'k': return KeepAlive;  // [K]eep-Alive
                        case 's': return SetCookie;  // [S]et-Cookie
                        case 'u': return UserAgent;  // [U]ser-Agent
                    }
                    break;

                case 11:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'c': return ContentMD5; // [C]ontent-MD5
                        case 'g': return GrpcStatus; // [g]rpc-status
                        case 'r': return RetryAfter; // [R]etry-After
                        case 's': return SetCookie2; // [S]et-Cookie2
                    }
                    break;

                case 12:
                    switch (KeyAt(ref key, 5) | 0x20)
                    {
                        case 'd': return XMSEdgeRef;  // X-MSE[d]ge-Ref
                        case 'e': return XPoweredBy;  // X-Pow[e]red-By
                        case 'm': return GrpcMessage; // grpc-[m]essage
                        case 'n': return ContentType; // Conte[n]t-Type
                        case 'o': return MaxForwards; // Max-F[o]rwards
                        case 't': return AcceptPatch; // Accep[t]-Patch
                        case 'u': return XRequestID;  // X-Req[u]est-ID
                    }
                    break;

                case 13:
                    switch (KeyAt(ref key, 12) | 0x20)
                    {
                        case 'd': return LastModified;  // Last-Modifie[d]
                        case 'e': return ContentRange;  // Content-Rang[e]
                        case 'g':
                            switch (KeyAt(ref key, 0) | 0x20)
                            {
                                case 's': return ServerTiming;  // [S]erver-Timin[g]
                                case 'g': return GrpcEncoding;  // [g]rpc-encodin[g]
                            }
                            break;
                        case 'h': return IfNoneMatch;   // If-None-Matc[h]
                        case 'l': return CacheControl;  // Cache-Contro[l]
                        case 'n': return Authorization; // Authorizatio[n]
                        case 's': return AcceptRanges;  // Accept-Range[s]
                        case 't': return ProxySupport;  // Proxy-Suppor[t]
                    }
                    break;

                case 14:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'a': return AcceptCharset; // [A]ccept-Charset
                        case 'c': return ContentLength; // [C]ontent-Length
                    }
                    break;

                case 15:
                    switch (KeyAt(ref key, 7) | 0x20)
                    {
                        case '-': return XFrameOptions;  // X-Frame[-]Options
                        case 'e': return AcceptEncoding; // Accept-[E]ncoding
                        case 'k': return PublicKeyPins;  // Public-[K]ey-Pins
                        case 'l': return AcceptLanguage; // Accept-[L]anguage
                        case 'm': return XUACompatible;  // X-UA-Co[m]patible
                        case 'r': return ReferrerPolicy; // Referre[r]-Policy
                    }
                    break;

                case 16:
                    switch (KeyAt(ref key, 11) | 0x20)
                    {
                        case 'a': return ContentLocation; // Content-Loc[a]tion
                        case 'c':
                            switch (KeyAt(ref key, 0) | 0x20)
                            {
                                case 'p': return ProxyConnection; // [P]roxy-Conne[c]tion
                                case 'x': return XXssProtection;  // [X]-XSS-Prote[c]tion
                            }
                            break;
                        case 'g': return ContentLanguage; // Content-Lan[g]uage
                        case 'i': return WWWAuthenticate; // WWW-Authent[i]cate
                        case 'o': return ContentEncoding; // Content-Enc[o]ding
                        case 'r': return XAspNetVersion;  // X-AspNet-Ve[r]sion
                    }
                    break;

                case 17:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'i': return IfModifiedSince;  // [I]f-Modified-Since
                        case 's': return SecWebSocketKey;  // [S]ec-WebSocket-Key
                        case 't': return TransferEncoding; // [T]ransfer-Encoding
                    }
                    break;

                case 18:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'p': return ProxyAuthenticate; // [P]roxy-Authenticate
                        case 'x': return XContentDuration;  // [X]-Content-Duration
                    }
                    break;

                case 19:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'c': return ContentDisposition; // [C]ontent-Disposition
                        case 'i': return IfUnmodifiedSince;  // [I]f-Unmodified-Since
                        case 'p': return ProxyAuthorization; // [P]roxy-Authorization
                    }
                    break;

                case 20:
                    return SecWebSocketAccept; // Sec-WebSocket-Accept

                case 21:
                    return SecWebSocketVersion; // Sec-WebSocket-Version

                case 22:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 'a': return AccessControlMaxAge;  // [A]ccess-Control-Max-Age
                        case 's': return SecWebSocketProtocol; // [S]ec-WebSocket-Protocol
                        case 'x': return XContentTypeOptions;  // [X]-Content-Type-Options
                    }
                    break;

                case 23:
                    return ContentSecurityPolicy; // Content-Security-Policy

                case 24:
                    return SecWebSocketExtensions; // Sec-WebSocket-Extensions

                case 25:
                    switch (KeyAt(ref key, 0) | 0x20)
                    {
                        case 's': return StrictTransportSecurity; // [S]trict-Transport-Security
                        case 'u': return UpgradeInsecureRequests; // [U]pgrade-Insecure-Requests
                    }
                    break;

                case 27:
                    return AccessControlAllowOrigin; // Access-Control-Allow-Origin

                case 28:
                    switch (KeyAt(ref key, 21) | 0x20)
                    {
                        case 'h': return AccessControlAllowHeaders; // Access-Control-Allow-[H]eaders
                        case 'm': return AccessControlAllowMethods; // Access-Control-Allow-[M]ethods
                    }
                    break;

                case 29:
                    return AccessControlExposeHeaders; // Access-Control-Expose-Headers

                case 32:
                    return AccessControlAllowCredentials; // Access-Control-Allow-Credentials
            }

            return null;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            static char KeyAt(ref byte key, nint offset) => (char)Unsafe.AddByteOffset(ref key, offset);
        }
    }
}