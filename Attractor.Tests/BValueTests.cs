using System.Numerics;

namespace Attractor.Tests;

public class BValueTests
{
    [Theory]
    [InlineData("")]
    [InlineData("spam")]
    public void StringParsingWorks(string expected)
    {
        var expectedEncoded = StringAsStream($"{expected.Length}:{expected}");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT0.AsString();

        Assert.Equivalent(expected, actual);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(-3)]
    public void IntegerParsingWorks(BigInteger expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT1;

        Assert.Equivalent(expected, actual);
    }

    [Theory]
    [InlineData()]
    [InlineData("4:spam", "4:eggs")]
    [InlineData("i3e", "i-3e")]
    [InlineData("4:rand", "i0e")]
    // FIXME(Unvailable): `xUnit` is very confused when comparing "nested" `BValue`.
    //
    // [InlineData("l5:choree", "l3:yese")]
    // [InlineData("4:rand", "i0e", "li5ee")]
    public void ListParsingWorks(params string[] expectedList)
    {
        var expected = expectedList.Select((x) => BValue.Parse(StringAsStream(x)).AsT0.Value);
        var expectedJoin = string.Join("", expectedList);
        var expectedEncoded = StringAsStream($"l{expectedJoin}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT2.Select((x) => x.Value);

        Assert.Equivalent(expected, actual);
    }

    // TODO(Unavailable): DictionnaryParsingWorks()

    static MemoryStream StringAsStream(string msg) => new(System.Text.Encoding.UTF8.GetBytes(msg));
}
