using Shouldly;

namespace Attractor.Tests;

// TODO(Unavailable): Indicate in the file name that these are integration tests?
public class TorrentTests
{
    [Theory]
    [InlineData("Resources/sample.torrent")]
    public async Task ItWorks(string path)
    {
        using var file = System.IO.File.OpenText(path);
        var torrent = Torrent.Parse(file.BaseStream).AsT0;

        torrent.Announce.AbsoluteUri.ShouldBe(
            "http://bittorrent-test-tracker.codecrafters.io/announce"
        );
        var length = torrent.Info.FileKind.AsT0.Length;
        length.ShouldBe(92063);
        Convert
            .ToHexStringLower(torrent.Info.Hash())
            .ShouldBe("d69f91e6b2ae4c542468d1073a71d4ea13879a7f");
        torrent.Info.PieceLength.ShouldBe(32768);
        torrent
            .Info.Pieces.Select((x) => Convert.ToHexStringLower(x.Bytes))
            .ShouldBe(
                [
                    "e876f67a2a8886e8f36b136726c30fa29703022d",
                    "6e2275e604a0766656736e81ff10b55204ad8d35",
                    "f00d937a0213df1982bc8d097227ad9e909acc17",
                ]
            );

        var peerId = System.Text.Encoding.UTF8.GetBytes("00112233445566778899");
        TrackerRequest request = new(torrent.Info.Hash(), peerId, (ulong)length);

        var response = await request.GetAsync(torrent.Announce);
        var responseOk = response.AsT0;

        responseOk.Interval.ShouldBe(60);
        responseOk
            .Peers.Select((peer) => $"{peer.Ip.AsT1}:{peer.Port}")
            .ShouldBeSubsetOf(
                ["165.232.41.73:51556", "165.232.38.164:51493", "165.232.35.114:51476"]
            );
    }

    // TEST(Unavailable): 'Fails' cases.
}
