using System.Diagnostics;
using System.Numerics;
using Shouldly;

namespace Attractor.Tests;

public class BValueTests
{
    [Theory]
    [InlineData("")]
    [InlineData("spam")]
    [InlineData("supercalifragilisticexpialidocious")]
    public void StringParsingWorks(string expected)
    {
        var expectedEncoded = StringAsStream($"{expected.Length}:{expected}");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT0.AsString();

        actual.ShouldBe(expected);
    }

    [Fact]
    public void StringParsingFails_WhenMissingColon()
    {
        var expectedEncoded = StringAsStream("4spam");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.MissingColon);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(-3)]
    public void IntegerParsingWorks(BigInteger expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var actual = BValue.Parse(expectedEncoded).AsT0.AsT1;

        actual.ShouldBe(expected);
    }

    [Fact]
    public void IntegerParsingFails_WhenEmptyInteger()
    {
        var expectedEncoded = StringAsStream("ie");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.EmptyInteger);
    }

    [Theory]
    [InlineData("01")]
    // TODO(Unavailable): These probably should be fall under
    // `IntegerParsingFails_WhenInvalidInteger`.
    [InlineData("0-0")]
    [InlineData("0xA")]
    public void IntegerParsingFails_LeadingZero(string expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.LeadingZeros);
    }

    [Fact]
    public void IntegerParsingFails_WhenMinusZero()
    {
        var expectedEncoded = StringAsStream("i-0e");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.MinusZero);
    }

    [Theory]
    [InlineData("(0)")]
    [InlineData(" 0 ")]
    [InlineData("+10")]
    public void IntegerParsingFails_WhenInvalidInteger(string expected)
    {
        var expectedEncoded = StringAsStream($"i{expected}e");
        var error = BValue.Parse(expectedEncoded).AsT1.AsT1;

        error.Message.ShouldContain("Invalid integer format");
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
    public void DictionaryParsingWorks(params string[] expectedList)
    {
        var expected = expectedList
            .Chunk(2)
            .Select(
                static (x) =>
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

        // UPSTREAM(Unavailable): Why `ShouldBe` isn't implemented for
        // `IEnumerable<KeyValuePair<_, _>>`?
        foreach (var (key, value) in expected)
        {
            actual.Values.ShouldContainKeyAndValue(key, value);
        }
    }

    [Theory]
    [InlineData("di0e4:spame")]
    [InlineData("dle2:me")]
    [InlineData("dd0:leei5ee")]
    public void DictionaryParsingFails_WhenKeyIsNotString(string expected)
    {
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.KeyIsNotString);
    }

    [Theory]
    [InlineData("d1:zi0e1:yi0ee")]
    [InlineData("d1:8le2:11i0ee")]
    public void DictionaryParsingFails_WhenUnorderedKeys(string expected)
    {
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.UnorderedKeys);
    }

    [Fact]
    public void DictionaryParsingFails_WhenMissingDictionaryValue()
    {
        var expectedEncoded = StringAsStream("d0:e");
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.MissingDictionaryValues(""));
    }

    [Theory]
    [InlineData("plide")]
    [InlineData("norde")]
    public void ParsingFails_WhenInvalidPrefixChar(string expected)
    {
        var expectedPrefix = expected.First();
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.InvalidPrefixChar(expectedPrefix));
    }

    [Theory]
    [InlineData("i128")]
    [InlineData("l3:370")]
    [InlineData("d1:a0:")]
    [InlineData("d1:xli0ee")]
    public void ParsingFails_WhenUnclosedPrefix(string expected)
    {
        var expectedPrefix = expected.First();
        var expectedEncoded = StringAsStream(expected);
        var error = BValue.Parse(expectedEncoded).AsT1;

        error.ShouldBe(ParsingError.UnclosedPrefix(expectedPrefix));
    }

    static MemoryStream StringAsStream(string msg) => new(System.Text.Encoding.UTF8.GetBytes(msg));
}
