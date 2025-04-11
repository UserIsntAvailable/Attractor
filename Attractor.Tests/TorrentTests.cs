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
    }

    // TEST(Unavailable): Write 'Fails' cases.
}
