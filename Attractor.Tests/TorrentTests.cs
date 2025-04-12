using Shouldly;

namespace Attractor.Tests;

public class TorrentTests
{
    [Theory]
    [InlineData("Resources/sample.torrent")]
    public void ItWorks(string path)
    {
        using var file = System.IO.File.OpenText(path);
        var torrent = Torrent.Parse(file.BaseStream).AsT0;

        torrent.Announce.AbsoluteUri.ShouldBe(
            "http://bittorrent-test-tracker.codecrafters.io/announce"
        );
        torrent.Info.FileKind.AsT0.Length.ShouldBe(92063);
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
    }

    // TEST(Unavailable): 'Fails' cases.
}
