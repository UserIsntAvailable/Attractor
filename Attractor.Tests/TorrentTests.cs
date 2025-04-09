using Shouldly;

namespace Attractor.Tests;

public class TorrentTests
{
    [Theory]
    [InlineData("Resources/sample.torrent")]
    public void ItWorks(string path)
    {
        var fullPath = path;

        using var file = System.IO.File.OpenText(fullPath);
        var torrent = Torrent.Parse(file.BaseStream).AsT0;

        torrent.Announce.AbsoluteUri.ShouldBe(
            "http://bittorrent-test-tracker.codecrafters.io/announce"
        );
        torrent.Info.Kind.AsT0.Length.ShouldBe(92063);
    }
}
