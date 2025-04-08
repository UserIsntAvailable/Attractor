using System.Diagnostics;
using System.Numerics;
using Shouldly;

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

        actual.ShouldBeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("4spam")]
    public void StringParsingFails_WhenMissingColon(string expected)
    {
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.Value.ShouldBeOfType<InvalidFormat>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-3)]
    public void IntegerParsingWorks(BigInteger expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT1;

        actual.ShouldBeEquivalentTo(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("-0")]
    [InlineData("-00")]
    [InlineData("0-0")]
    [InlineData("(0)")]
    [InlineData(" 0 ")]
    [InlineData("010")]
    [InlineData("+10")]
    [InlineData("0xA")]
    public void IntegerParsingFails(string expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.Value.ShouldBeOfType<InvalidFormat>();
    }

    [Theory]
    [InlineData()]
    [InlineData("4:spam", "4:eggs")]
    [InlineData("i3e", "i-3e")]
    [InlineData("l5:choree", "l3:yese")]
    [InlineData("4:rand", "i0e", "li5ee")]
    public void ListParsingWorks(params string[] expectedList)
    {
        var expected = expectedList.Select((x) => BValue.Parse(StringAsStream(x)).AsT0);
        var expectedJoin = string.Join("", expectedList);
        var expectedEncoded = StringAsStream($"l{expectedJoin}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT2;

        actual.ShouldBe(expected);
    }

    [Theory]
    [InlineData()]
    [InlineData("3:cow", "3:moo", "4:spam", "4:eggs")]
    [InlineData("4:spam", "l1:a1:bee")]
    public void DictionnaryParsingWorks(params string[] expectedList)
    {
        var expected = expectedList
            .Chunk(2)
            .Select(
                (x) =>
                {
                    if (x is [var key, var value])
                    {
                        var keyBValue = BValue.Parse(StringAsStream(key)).AsT0.AsT0;
                        var valueBValue = BValue.Parse(StringAsStream(value)).AsT0;
                        return (keyBValue, valueBValue);
                    }
                    throw new UnreachableException();
                }
            );
        var expectedJoin = string.Join("", expectedList);
        var expectedEncoded = StringAsStream($"d{expectedJoin}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT3;

        // UPSTREAM(Unvailable): Why `ShouldBe` isn't implemented for
        // `IEnumerable<KeyValuePair<_, _>>`?
        foreach (var (key, value) in expected)
        {
            actual.Values.ShouldContainKeyAndValue(key, value);
        }
    }

    [Theory]
    [InlineData("plide")]
    [InlineData("norde")]
    public void ParsingFails_WhenInvalidPrefixChar(string expected)
    {
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.Value.ShouldBeOfType<InvalidFormat>();
    }

    [Theory]
    [InlineData("i128")]
    [InlineData("l3:370")]
    [InlineData("d1:a0:")]
    [InlineData("d1:xli0ee")]
    public void ParsingFails_WhenUnclosedPrefix(string expected)
    {
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.Value.ShouldBeOfType<InvalidFormat>();
    }

    static MemoryStream StringAsStream(string msg) => new(System.Text.Encoding.UTF8.GetBytes(msg));
}