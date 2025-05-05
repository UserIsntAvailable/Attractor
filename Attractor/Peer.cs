using System.Net;

namespace Attractor;

public record Peer(IPEndPoint EndPoint, byte[]? Id = null)
{
    public Peer(IPAddress address, ushort port)
        : this(new(address, port)) { }

    public IPAddress Address => EndPoint.Address;

    public int Port => EndPoint.Port;
}