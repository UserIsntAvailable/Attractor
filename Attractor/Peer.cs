using System.Net;
using OneOf;

namespace Attractor;

// TODO(Unavailable): Resolve `Uri`, and just store a `IPEndPoint`?
public record Peer(
    // FIXME(Unavailable): `Uri` is technically not right, because it should
    // only contain a domain name.
    OneOf<Uri, IPAddress> Ip,
    ushort Port,
    byte[]? Id = null
) { }
