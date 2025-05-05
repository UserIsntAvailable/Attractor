#pragma warning disable CS8851

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using OneOf;
using OneOf.Types;

// FIXME(Unavailable): I need to namespace this project's types better...
namespace Attractor;

/// <summary>
/// A benconded value
/// </summary>
[GenerateOneOf]
public partial class BValue : OneOfBase<BString, BigInteger, BList, BDictionary>
{
    public BValue(string byteString)
        : base(new BString(byteString)) { }

    public BValue(byte[] bytes)
        : base(new BString(bytes)) { }

    public BValue(List<BValue> values)
        : base(new BList(values)) { }

    public BValue(SortedDictionary<BString, BValue> values)
        : base(new BDictionary(values)) { }

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
    public static OneOf<BValue, ParsingError> Parse(
        Stream stream,
        BValueParseOptions? opts = default
    )
    {
        ArgumentNullException.ThrowIfNull(stream);

        using BinaryReader reader = new(stream, Encoding.UTF8, true);
        return Parse(reader, opts ?? new());
    }

    public OneOf<None, IOException> Encode(Stream stream)
    {
        // FIXME(Unavailable): try/catch and properly propagate recursive
        // exception errors.
        return Match(
            (str) =>
            {
                stream.Write(
                    [
                        .. Encoding.UTF8.GetBytes(str.Bytes.Length.ToString()),
                        (byte)':',
                        .. str.Bytes,
                    ]
                );
                return new None();
            },
            (@int) =>
            {
                stream.Write([(byte)'i', .. Encoding.UTF8.GetBytes(@int.ToString()), (byte)'e']);
                return new None();
            },
            (list) =>
            {
                stream.Write([(byte)'l']);
                foreach (var elem in list)
                {
                    elem.Encode(stream);
                }
                stream.Write([(byte)'e']);
                return new None();
            },
            (dict) =>
            {
                stream.Write([(byte)'d']);
                foreach (var (key, val) in dict)
                {
                    new BValue(key).Encode(stream);
                    val.Encode(stream);
                }
                stream.Write([(byte)'e']);
                return new None();
            }
        );
    }
}

public partial class BValue
{
    private static OneOf<BValue, ParsingError> Parse(BinaryReader reader, BValueParseOptions opts)
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
            'l' => ParseList(reader, opts),
            'd' => ParseDictionary(reader, opts),
            _ => ParsingError.InvalidPrefixChar(prefix),
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

        return new BValue(bytes);
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

        return chars switch
        {
            [] => ParsingError.EmptyInteger,
            ['0', _, ..] => ParsingError.LeadingZeros,
            // FIXME(Unavailable): Both `LeadingZeros` and `MinusZero` should
            // be returned with `-00`.
            ['-', '0', ..] => ParsingError.MinusZero,
            _ => BigIntegerFromChars(chars).MapT0(static (x) => new BValue(x)),
        };
    }

    private static OneOf<BValue, ParsingError> ParseList(
        BinaryReader reader,
        BValueParseOptions opts
    )
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

            if (Parse(reader, opts).TryPickT0(out var value, out var error))
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

        return new BValue(result);
    }

    private static OneOf<BValue, ParsingError> ParseDictionary(
        BinaryReader reader,
        BValueParseOptions opts
    )
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
            if (Parse(reader, opts).TryPickT1(out var keyError, out var key))
            {
                return keyError;
            }

            if (!key.TryPickT0(out var keyString, out var _))
            {
                return ParsingError.KeyIsNotString;
            }

            if (opts.CheckDictionaryKeyOrder && keyString < lastKey!)
            {
                return ParsingError.UnorderedKeys;
            }
            // FIXME(Unavailable): What should happen with key duplicates?
            lastKey = keyString;

            if (Parse(reader, opts).TryPickT0(out var value, out var valueError))
            {
                result.Add(keyString, value);
            }
            else
            {
                return valueError.Equals(ParsingError.InvalidPrefixChar('e'))
                    ? ParsingError.MissingDictionaryValues(keyString.ToString())
                    : valueError;
            }
        }

        if (peek != 'e')
        {
            return ParsingError.UnclosedPrefix('d');
        }

        return new BValue(result);
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

public record BValueParseOptions(bool CheckDictionaryKeyOrder = true) { }

public record BString(byte[] Bytes) : IComparable<BString>
{
    public BString(string byteString)
        : this(Encoding.UTF8.GetBytes(byteString)) { }

    public override string ToString()
    {
        return Bytes is null ? "" : Encoding.UTF8.GetString([.. Bytes]);
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