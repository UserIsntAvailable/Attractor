﻿#pragma warning disable CS8851

using System.Collections;
using System.Globalization;
using System.Numerics;
using OneOf;

namespace Attractor;

/// <summary>
/// A benconded value
/// </summary>
[GenerateOneOf]
public partial class BValue : OneOfBase<BString, BigInteger, BList, BDictionnary>
{
    // TODO(Unavailable): async methods

    // TODO(Unavailable): Simplify unclosed prefix reading to avoid
    // `if (peek/read != 'e')`.

    // DOCS(Unavailable): Encourage to buffer the stream to optimize performance.

    // FIXME(Unavailable): The `Stream` needs to support seeking (which is fixed
    // by using a BufferedStream, I think).

    // FIXME(Unavailable): Catch all exceptions as `UnexpectedEof`.

    /// <summary>
    /// Parses a <see cref="BValue"/> from the <paramref name="stream"/>.
    /// </summary>
    public static OneOf<BValue, Error> Parse(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        BinaryReader reader = new(stream);

        return Parse(reader);
    }

    private static OneOf<BValue, Error> Parse(BinaryReader reader)
    {
        int value;
        if ((value = reader.Read()) == -1)
        {
            return Error.UnexpectedEof;
        }

        return (char)value switch
        {
            >= '0' and <= '9' => ParseString(value - '0', reader),
            'i' => ParseBigInteger(reader),
            'l' => ParseList(reader),
            'd' => ParseDictionnary(reader),
            _ => Error.InvalidPrefixChar,
        };
    }

    private static OneOf<BValue, Error> ParseString(int length, BinaryReader reader)
    {
        if (reader.Read() != ':')
        {
            return Error.InvalidFormat;
        }

        byte[] bytes = reader.ReadBytes(length);

        if (bytes.Length != length)
        {
            return Error.UnexpectedEof;
        }

        return new BValue(new BString(bytes));
    }

    private static OneOf<BValue, Error> ParseBigInteger(BinaryReader reader)
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

        // invalid operations:
        if (
            (
                chars
                is
                    // `ie`
                    []
                    // leading zeros
                    or ['0', _, ..]
                    // -0
                    or ['-', '0', ..]
            )
            // unclosed prefix
            || read != 'e'
        )
        {
            return Error.InvalidFormat;
        }

        NumberStyles style = NumberStyles.AllowLeadingSign;
        NumberFormatInfo provider = new() { PositiveSign = "" };

        return BigInteger.TryParse(chars.ToArray(), style, provider, out var bint)
            ? new BValue(bint)
            : Error.InvalidFormat;
    }

    private static OneOf<BValue, Error> ParseList(BinaryReader reader)
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
            return Error.InvalidFormat;
        }

        return new BValue(new BList(result));
    }

    private static OneOf<BValue, Error> ParseDictionnary(BinaryReader reader)
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

            // FIXME(Unvailable): This is _very_ bad, because we only need to
            // check if string parsing works.
            if (Parse(reader).TryPickT1(out var keyError, out var key))
            {
                return keyError;
            }

            if (!key.TryPickT0(out var keyString, out var _))
            {
                return Error.InvalidFormat;
            }

            if (Parse(reader).TryPickT0(out var value, out var valueError))
            {
                result.Add(keyString, value);
            }
            else
            {
                return valueError;
            }
        }

        if (peek != 'e')
        {
            return Error.InvalidFormat;
        }

        return new BValue(new BDictionnary(result));
    }
}

// TODO(Unavailable): `IsUTF8` property hint (I think it can be useful for
// BDictionary keys).
public record BString(byte[] Bytes) : IComparable<BString>
{
    public string AsString()
    {
        return System.Text.Encoding.UTF8.GetString([.. Bytes]);
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

public record BDictionnary(SortedDictionary<BString, BValue> Values)
    : IEnumerable<KeyValuePair<BString, BValue>>
{
    public virtual bool Equals(BDictionnary? other)
    {
        return other != null && Values.SequenceEqual(other.Values);
    }

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

[GenerateOneOf]
public partial class Error : OneOfBase<InvalidPrefixChar, InvalidFormat, UnexpectedEof>
{
    // TODO(Unavailable): Move into `InvalidFormat`.
    internal static readonly Error InvalidPrefixChar = new InvalidPrefixChar();
    internal static readonly Error InvalidFormat = new InvalidFormat();
    internal static readonly Error UnexpectedEof = new UnexpectedEof();
}

// TODO(Unavailable): Char property
public readonly record struct InvalidPrefixChar();

// TODO(Unvailable): Message property reasons:
//
// - Missing colon for string parsing
// - -0
// - Leading zeros
// - Unclosed i,l,d prefixes
// - Expected string for dictionary key
// - Missing value for dictionary
//
// (don't forget to write tests for each variant once the are added :))
public readonly record struct InvalidFormat();

public readonly record struct UnexpectedEof();
