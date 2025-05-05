using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using OneOf;

namespace Attractor;

/// <param name="InfoHash">
/// The 20 byte sha1 hash of the bencoded form of the info value from the
/// metainfo file. This value will almost certainly have to be escaped.
///
/// Note that this is a substring of the metainfo file. The info-hash must be
/// the hash of the encoded form as found in the .torrent file, which is
/// identical to bdecoding the metainfo file, extracting the info dictionary
/// and encoding it if and only if the bdecoder fully validated the input (e.g.
/// key ordering, absence of leading zeros). Conversely that means clients must
/// either reject invalid metainfo files or extract the substring directly. They
/// must not perform a decode-encode roundtrip on invalid data.
/// </param>
///
/// <param name="PeerId">
/// A string of length 20 which this downloader uses as its id. Each downloader
/// generates its own id at random at the start of a new download. This value
/// will also almost certainly have to be escaped.
/// </param>
///
/// <param name="Ip">
/// An optional parameter giving the IP (or dns name) which this peer is at.
/// Generally used for the origin if it's on the same machine as the tracker.
/// </param>
///
/// <param name="Port">
/// The port number this peer is listening on. Common behavior is for a
/// downloader to try to listen on port 6881 and if that port is taken try 6882,
/// then 6883, etc. and give up after 6889.
/// </param>
///
/// <param name="Uploaded">
/// The total amount uploaded so far.
/// </param>
///
/// <param name="Downloaded">
/// The total amount downloaded so far.
/// </param>
///
/// <param name="Left">
/// The number of bytes this peer still has to download. Note that this can't be
/// computed from downloaded and the file length since it might be a resume, and
/// there's a chance that some of the downloaded data failed an integrity check
/// and had to be re-downloaded.
/// </param>
///
/// <param name="Event">
/// This is an optional key which maps to started, completed, or stopped (or
/// empty, which is the same as not being present). If not present, this is one
/// of the announcements done at regular intervals. An announcement using
/// started is sent when a download first begins, and one using completed is
/// sent when the download is complete. No completed is sent if the file was
/// complete when started. Downloaders send an announcement using stopped when
/// they cease downloading.
/// </param>
///
/// <param name="Compact">
/// Wether the peer list returned on the response should use the compact
/// representation.
///
/// It is SUGGESTED that trackers return compact format by default. By including
/// 'False' in the announce URL, the client advises the tracker that is prefers
/// the list format, and analogously 'True' advises the tracker that the client
/// prefers the string compact format. However the compact key-value pair is
/// only advisory: the tracker MAY return using either format. compact is
/// advisory so that trackers may support only the compact format. However,
/// clients MUST continue to support both.
/// </param>

// TODO(Unavailable): `ulong` should really be `UBigInteger`
public record class TrackerRequest(
    byte[] InfoHash,
    byte[] PeerId,
    ulong Left,
    IPAddress? Ip = default,
    ushort Port = 6881,
    ulong Uploaded = 0,
    ulong Downloaded = 0,
    TrackerRequestEvent Event = TrackerRequestEvent.Empty,
    bool Compact = true
)
{
    // FIXME(Unavailable): try/catch and properly propagate errors.
    public async Task<TrackerResponse> GetAsync(Uri announce)
    {
        using HttpClient client = new();

        // FIXME(Unavailable): `announce` could already have query params.
        var trackerUrl = announce.AbsoluteUri + ToQueryString();
        var request = await client.GetAsync(trackerUrl);
        var stream = await request.Content.ReadAsStreamAsync();

        using BufferedStream bufStream = new(stream, 128);
        // NOTE(Unavailable): The codecrafters tracker returns dictionaries
        // where the required keys are ordered, but the optional/extension keys
        // are just appended to the end, without properly checking their order;
        // if codecrafters is doing this wrong, then I could assume that others
        // are also doing it wrong...
        BValueParseOptions opts = new(CheckDictionaryKeyOrder: false);
        var response = BValue.Parse(bufStream, opts).AsT0.AsT3;

        var interval = response["interval"];
        if (interval is not null)
        {
            var intervalInteger = interval.AsT1;
            var peersBytes = response["peers"]!.AsT0;

            // FIXME(Unavailable): `Compact=True` is only an advisory flag.
            if (Compact)
            {
                var peers = peersBytes
                    .Bytes.Chunk(6)
                    .Select(
                        static (bytes) =>
                            new Peer(
                                new IPAddress(bytes.AsSpan()[..4]),
                                BinaryPrimitives.ReadUInt16BigEndian(bytes.AsSpan()[4..])
                            )
                    )
                    .ToList();
                return new TrackerResponseOk((uint)intervalInteger, peers);
            }
            else
            {
                // FIXME(Unavailable): Ignoring `Compact=False` for now...
                throw new NotImplementedException();
            }
        }
        else
        {
            return new TrackerResponseErr(response["failure"]!.AsT0.ToString());
        }
    }

    internal string ToQueryString()
    {
        var infoHashEncoded = HttpUtility.UrlEncode(InfoHash);
        var peerIdEncoded = HttpUtility.UrlEncode(PeerId);

        StringBuilder sb = new("?", 152);

        _ = sb.Append($"info_hash={infoHashEncoded}");
        _ = sb.Append($"&peer_id={peerIdEncoded}");
        if (Ip is not null)
        {
            _ = sb.Append($"&ip={Ip}");
        }
        _ = sb.Append($"&port={Port}");
        _ = sb.Append($"&uploaded={Uploaded}");
        _ = sb.Append($"&downloaded={Downloaded}");
        _ = sb.Append($"&left={Left}");
        if (Event is not TrackerRequestEvent.Empty)
        {
#pragma warning disable CS8524,IDE0072
            var eventMsg = Event switch
#pragma warning restore CS8524,IDE0072
            {
                TrackerRequestEvent.Started => "started",
                TrackerRequestEvent.Completed => "completed",
                TrackerRequestEvent.Stopped => "stopped",
                TrackerRequestEvent.Empty => throw new UnreachableException(),
            };
            _ = sb.Append($"&event={eventMsg}");
        }
        _ = sb.Append($"&compact={(Compact ? 1 : 0)}");

        return sb.ToString();
    }
}

public enum TrackerRequestEvent
{
    Empty,
    Started,
    Completed,
    Stopped,
}

[GenerateOneOf]
public partial class TrackerResponse : OneOfBase<TrackerResponseOk, TrackerResponseErr> { }

/// An successful response returned by the tracker.
///
/// <param name="Interval">
/// The number of seconds the downloader should wait between regular rerequests.
///
/// Note that downloaders may rerequest on nonscheduled times if an event
/// happens or they need more peers.
/// </param>
///
/// <param name="Peers">
/// A list of <see cref="Peer"/>s where torrent pieces can be
/// </param>
public record TrackerResponseOk(uint Interval, List<Peer> Peers) { }

/// An error response returned by the tracker.
///
/// <param name="Failure">
/// Explains why the response failed.
/// </param>
public record TrackerResponseErr(string Failure) { }