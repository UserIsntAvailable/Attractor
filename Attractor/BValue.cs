﻿#pragma warning disable CS8851

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

    // FIXME(Unavailable): Properly try/catch exceptions.

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
            return ParsingError.EndOfStream;
        }

        var prefix = (char)value;
        return prefix switch
        {
            >= '0' and <= '9' => ParseString(prefix, reader),
            'i' => ParseBigInteger(reader),
            'l' => ParseList(reader),
            'd' => ParseDictionary(reader),
            _ => ParsingError.InvalidPrefixChar((char)value),
        };
    }

    private static OneOf<BValue, ParsingError> ParseString(char firstDigit, BinaryReader reader)
    {
        List<char> chars = new(10) { firstDigit };

        int read;
        while ((read = reader.Read()) != -1)
        {
            if (read == ':')
            {
                break;
            }
            chars.Add((char)read);
        }

        if (read != ':')
        {
            return ParsingError.MissingColon;
        }
        if (BigIntegerFromChars(chars).TryPickT1(out var error, out var length))
        {
            return ParsingError.FormatException("Invalid string length prefix.", error);
        }

        // FIXME(Unavailable): strings can have length higher than `int.MAX`.
        byte[] bytes = reader.ReadBytes((int)length);

        if (bytes.Length != length)
        {
            return ParsingError.EndOfStream;
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
            return ParsingError.UnclosedPrefix('i');
        }
        switch (chars)
        {
            case []:
                return ParsingError.EmptyInteger;
            case ['0', _, ..]:
                return ParsingError.LeadingZeros;
            // FIXME(Unavailable): Both `LeadingZeros` and `MinusZero` should be returned with
            // `-00`.
            case ['-', '0', ..]:
                return ParsingError.MinusZero;
        }

        return BigIntegerFromChars(chars).MapT0((x) => new BValue(x));
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
            return ParsingError.UnclosedPrefix('l');
        }

        return new BValue(new BList(result));
    }

    private static OneOf<BValue, ParsingError> ParseDictionary(BinaryReader reader)
    {
        SortedDictionary<BString, BValue> result = [];
        BString? lastKey = default;

        int peek;
        while ((peek = reader.PeekChar()) != -1)
        {
            if (peek == 'e')
            {
                _ = reader.Read();
                break;
            }

            // FIXME(Unavailable): This is _very_ bad, because we only need to
            // check if string parsing works.
            if (Parse(reader).TryPickT1(out var keyError, out var key))
            {
                return keyError;
            }

            if (!key.TryPickT0(out var keyString, out var _))
            {
                return ParsingError.KeyIsNotString;
            }

            if (keyString < lastKey!)
            {
                return ParsingError.UnorderedKeys;
            }
            // FIXME(Unavailable): What should happen with key duplicates?
            lastKey = keyString;

            if (Parse(reader).TryPickT0(out var value, out var valueError))
            {
                result.Add(keyString, value);
            }
            else
            {
                return valueError.Equals(ParsingError.InvalidPrefixChar('e'))
                    ? ParsingError.MissingDictionaryValues(keyString.AsString())
                    : valueError;
            }
        }

        if (peek != 'e')
        {
            return ParsingError.UnclosedPrefix('d');
        }

        return new BValue(new BDictionary(result));
    }

    private static OneOf<BigInteger, ParsingError> BigIntegerFromChars(List<char> chars)
    {
        try
        {
            NumberStyles style = NumberStyles.AllowLeadingSign;
            NumberFormatInfo provider = new() { PositiveSign = "" };
            return BigInteger.Parse(chars.ToArray(), style, provider);
        }
        catch (FormatException ex)
        {
            return ParsingError.FormatException("Invalid integer format", ex);
        }
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
        return other is not null && Bytes.AsSpan().SequenceEqual(other.Bytes);
    }

    public int CompareTo(BString? other)
    {
        return other is null ? 1 : Bytes.AsSpan().SequenceCompareTo(other!.Bytes);
    }

    public static bool operator >(BString self, BString other)
    {
        return self.CompareTo(other) > 0;
    }

    public static bool operator >=(BString self, BString other)
    {
        return self.CompareTo(other) >= 0;
    }

    public static bool operator <(BString self, BString other)
    {
        return self.CompareTo(other) < 0;
    }

    public static bool operator <=(BString self, BString other)
    {
        return self.CompareTo(other) <= 0;
    }
}

public record BList(List<BValue> Values) : IEnumerable<BValue>
{
    public virtual bool Equals(BList? other)
    {
        return other is not null && Values.SequenceEqual(other.Values);
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
        return other is not null && Values.SequenceEqual(other.Values);
    }

    public BValue? this[BString key] => Values.TryGetValue(key, out var value) ? value : null;

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

// TODO(Unavailable): Move to its own file.
// FIXME(Unavailable): Add `IOException` as one of the cases.
[GenerateOneOf]
public partial class ParsingError : OneOfBase<EndOfStreamException, FormatException>
{
    public Exception AsException()
    {
        return (Exception)Value;
    }

    public override int GetHashCode()
    {
        return Value.GetHashCode();
    }

    public override bool Equals(object obj)
    {
        return Equals(obj as ParsingError);
    }

    public virtual bool Equals(ParsingError? other)
    {
        return other is not null
            && (
                (IsT0 && other.IsT0 && AsT0.Message == other.AsT0.Message)
                || (IsT1 && other.IsT1 && AsT1.Message == other.AsT1.Message)
            );
    }

    // NOTE: Try to order these by the 'category' of the helper.

    internal static readonly ParsingError EndOfStream = new EndOfStreamException();

    internal static ParsingError FormatException(string message)
    {
        return new FormatException(message);
    }

    internal static ParsingError FormatException(string message, ParsingError error)
    {
        return new FormatException(message, error.AsException());
    }

    internal static ParsingError InvalidPrefixChar(char prefix)
    {
        return new ParsingError(new FormatException($"Invalid prefix character '{prefix}'."));
    }

    internal static ParsingError UnclosedPrefix(char prefix)
    {
        return new FormatException($"The prefix '{prefix}' wasn't closed with 'e'.");
    }

    internal static readonly ParsingError MissingColon = new FormatException(
        "Missing ':' separator for string prefix."
    );

    internal static readonly ParsingError EmptyInteger = new FormatException(
        "Empty integers (ie) are not valid."
    );

    internal static readonly ParsingError LeadingZeros = new FormatException(
        "Leading zeros (01) are not allowed on integers."
    );

    internal static readonly ParsingError MinusZero = new FormatException(
        "'-0' is not a valid integer value."
    );

    internal static readonly ParsingError KeyIsNotString = new FormatException(
        "Dictionary's keys can only be strings."
    );

    internal static readonly ParsingError UnorderedKeys = new FormatException(
        "Dictionary's keys should be ordered by the 'raw' bytes string representation."
    );

    internal static ParsingError MissingDictionaryValues(string key)
    {
        return new FormatException($"The dictionary key '{key}' is missing a value.");
    }
}
