#pragma warning disable CS8851

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using OneOf;

// TODO(Unavailable): Is there a way to do this from the `.csproj` file.
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Attractor.Tests")]

namespace Attractor;

/// <summary>
/// A benconded value
/// </summary>
[GenerateOneOf]
public partial class BValue : OneOfBase<BString, BigInteger, BList, BDictionary>
{
    // TODO(Unavailable): async methods.

    // TODO(Unavailable): Simplify unclosed prefix reading to avoid
    // `if (peek/read != 'e')`.

    // TODO(Unavailable): As/TryPick{BString, BigInteger, BList, BDictionary} conversions.

    // DOCS(Unavailable): Encourage to buffer the stream to optimize performance.

    // FIXME(Unavailable): The `Stream` needs to support seeking (which is fixed
    // by using a BufferedStream, I think).

    // FIXME(Unavailable): Catch all exceptions as `UnexpectedEof`.

    /// <summary>
    /// Parses a <see cref="BValue"/> from the <paramref name="stream"/>.
    /// </summary>
    public static OneOf<BValue, ParsingError> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        using BinaryReader reader = new(stream, Encoding.UTF8, true);
        return Parse(reader);
    }

    private static OneOf<BValue, ParsingError> Parse(BinaryReader reader)
    {
        int value;
        if ((value = reader.Read()) == -1)
        {
            return ParsingError.UnexpectedEof;
        }

        var prefix = (char)value;
        return prefix switch
        {
            >= '0' and <= '9' => ParseString(prefix, reader),
            'i' => ParseBigInteger(reader),
            'l' => ParseList(reader),
            'd' => ParseDictionary(reader),
            _ => InvalidFormatError.InvalidPrefixChar((char)value),
        };
    }

    private static OneOf<BValue, ParsingError> ParseString(char firstDigit, BinaryReader reader)
    {
        List<byte> bytesBuffer = new(10) { (byte)firstDigit };

        int read;
        while ((read = reader.Read()) != -1)
        {
            if (read == ':')
            {
                break;
            }
            bytesBuffer.Add((byte)read);
        }

        if (read != ':')
        {
            return InvalidFormatError.MissingColon;
        }

        // FIXME(Unavailable): Catch exception
        // FIXME(Unavailable): String can have length higher than `int.MAX`.
        // PERF(Unavailable): Is there really not a way to turn a `List` into
        // a `ReadOnlySpan`?
        var length = int.Parse(bytesBuffer.ToArray());

        byte[] bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            return ParsingError.UnexpectedEof;
        }

        return new BValue(new BString(bytes));
    }

    private static OneOf<BValue, ParsingError> ParseBigInteger(BinaryReader reader)
    {
        // most numbers are small
        List<char> chars = new(10);

        int read;
        while ((read = reader.Read()) != -1)
        {
            if (read == 'e')
            {
                break;
            }
            chars.Add((char)read);
        }

        if (read != 'e')
        {
            return InvalidFormatError.UnclosedPrefix('i');
        }
        switch (chars)
        {
            case []:
                return InvalidFormatError.EmptyInteger;
            case ['0', _, ..]:
                return InvalidFormatError.LeadingZeros;
            // FIXME(Unavailable): Both `LeadingZeros` and `MinusZero` should be returned with
            // `-00`.
            case ['-', '0', ..]:
                return InvalidFormatError.MinusZero;
        }

        try
        {
            NumberStyles style = NumberStyles.AllowLeadingSign;
            NumberFormatInfo provider = new() { PositiveSign = "" };
            return new BValue(BigInteger.Parse(chars.ToArray(), style, provider));
        }
        catch (FormatException ex)
        {
            return InvalidFormatError.Custom("Invalid integer format", ex);
        }
    }

    private static OneOf<BValue, ParsingError> ParseList(BinaryReader reader)
    {
        List<BValue> result = [];

        int peek;
        while ((peek = reader.PeekChar()) != -1)
        {
            if (peek == 'e')
            {
                _ = reader.Read();
                break;
            }

            if (Parse(reader).TryPickT0(out var value, out var error))
            {
                result.Add(value);
            }
            else
            {
                return error;
            }
        }

        if (peek != 'e')
        {
            return InvalidFormatError.UnclosedPrefix('l');
        }

        return new BValue(new BList(result));
    }

    private static OneOf<BValue, ParsingError> ParseDictionary(BinaryReader reader)
    {
        SortedDictionary<BString, BValue> result = [];

        int peek;
        while ((peek = reader.PeekChar()) != -1)
        {
            if (peek == 'e')
            {
                _ = reader.Read();
                break;
            }

            // FIXME(Unavailable): key ordering is indeed required while parsing.

            // FIXME(Unavailable): This is _very_ bad, because we only need to
            // check if string parsing works.
            if (Parse(reader).TryPickT1(out var keyError, out var key))
            {
                return keyError;
            }

            if (!key.TryPickT0(out var keyString, out var _))
            {
                return InvalidFormatError.KeyIsNotString;
            }

            if (Parse(reader).TryPickT0(out var value, out var valueError))
            {
                result.Add(keyString, value);
            }
            else
            {
                return
                    valueError.TryPickT1(out var formatError, out var _)
                    // TODO(Unavailable): InvalidFormatError.Equals() override
                    && formatError.Message == InvalidFormatError.InvalidPrefixChar('e').AsT1.Message
                    ? InvalidFormatError.MissingDictionaryValues(keyString.AsString())
                    : valueError;
            }
        }

        if (peek != 'e')
        {
            return InvalidFormatError.UnclosedPrefix('d');
        }

        return new BValue(new BDictionary(result));
    }
}

// TODO(Unavailable): `IsUTF8` property hint (I think it can be useful for
// BDictionary keys).
public record BString(byte[] Bytes) : IComparable<BString>
{
    public string AsString()
    {
        return Encoding.UTF8.GetString([.. Bytes]);
    }

    public virtual bool Equals(BString? other)
    {
        return other != null && Bytes.AsSpan().SequenceEqual(other.Bytes);
    }

    // TODO(Unavailable): overload `>`, `>=`, `<`, and `<=`.
    public int CompareTo(BString? other)
    {
        return other == null ? 1 : Bytes.AsSpan().SequenceCompareTo(other!.Bytes);
    }
}

public record BList(List<BValue> Values) : IEnumerable<BValue>
{
    public virtual bool Equals(BList? other)
    {
        return other != null && Values.SequenceEqual(other.Values);
    }

    public IEnumerator<BValue> GetEnumerator()
    {
        return Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public record BDictionary(SortedDictionary<BString, BValue> Values)
    : IEnumerable<KeyValuePair<BString, BValue>>
{
    public virtual bool Equals(BDictionary? other)
    {
        return other != null && Values.SequenceEqual(other.Values);
    }

    public BValue? this[BString key] => Values.TryGetValue(key, out var value) ? null : value;

    public BValue? this[string key] => this[new BString(Encoding.UTF8.GetBytes(key))];

    public IEnumerator<KeyValuePair<BString, BValue>> GetEnumerator()
    {
        return Values.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

// errors

// FIXME(Unavailable): Add `IOException` as one of the cases.
[GenerateOneOf]
public partial class ParsingError : OneOfBase<UnexpectedEofError, InvalidFormatError>
{
    // internal static readonly ParsingError InvalidFormat = new InvalidFormatError("");
    // FIXME(Unavailable): Reuse `EndOfStreamException`.
    internal static readonly ParsingError UnexpectedEof = new UnexpectedEofError();
}

public readonly record struct UnexpectedEofError;

// TODO(Unavailable): Should I reuse `FormatException`, and move the helper
// constructors into `ParsingError`?
//
// TODO(Unavailable): Implement `IEquatable<T>`.
//
// Remember to refactor the `error.Message.ShouldBe(...)` mess on `BValueTests`.
public class InvalidFormatError : Exception
{
    private InvalidFormatError(string? message)
        : base(message)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
    }

    private InvalidFormatError(string? message, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrEmpty(message);
    }

    // NOTE: Please ignore the order of these. They are supposed to be ordered
    // by how they appear on this file.

    internal static ParsingError InvalidPrefixChar(char prefix)
    {
        return new ParsingError(new InvalidFormatError($"Invalid prefix character '{prefix}'."));
    }

    internal static readonly ParsingError MissingColon = new InvalidFormatError(
        "Missing ':' separator for string prefix."
    );

    internal static readonly ParsingError EmptyInteger = new InvalidFormatError(
        "Empty integers (ie) are not valid."
    );

    internal static readonly ParsingError LeadingZeros = new InvalidFormatError(
        "Leading zeros (01) are not allowed on integers."
    );

    internal static readonly ParsingError MinusZero = new InvalidFormatError(
        "'-0' is not a valid integer value."
    );

    internal static ParsingError UnclosedPrefix(char prefix)
    {
        return new InvalidFormatError($"The prefix '{prefix}' wasn't closed with 'e'.");
    }

    internal static readonly ParsingError KeyIsNotString = new InvalidFormatError(
        "Dictionary's keys can only be strings."
    );

    internal static ParsingError MissingDictionaryValues(string key)
    {
        return new InvalidFormatError($"The dictionnary key '{key}' is missing a value.");
    }

    internal static ParsingError Custom(string message)
    {
        return new InvalidFormatError(message);
    }

    internal static ParsingError Custom(string message, Exception exception)
    {
        return new InvalidFormatError(message, exception);
    }
}
