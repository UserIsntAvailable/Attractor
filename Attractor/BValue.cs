#pragma warning disable CS8851

using System.Collections;
using System.Globalization;
using System.Numerics;
using System.Text;
using OneOf;
using OneOf.Types;

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

    public OneOf<IOException, None> Encode(Stream stream)
    {
        // FIXME(Unavailable): try/catch and properly propagate recursive
        // exception errors.
        return Match(
            (str) =>
            {
                stream.Write(
                    [
                        // FIXME(Unavailable): FormatProvider
                        .. Encoding.UTF8.GetBytes(str.Bytes.Length.ToString()),
                        (byte)':',
                        .. str.Bytes,
                    ]
                );
                return new None();
            },
            (@int) =>
            {
                // FIXME(Unavailable): FormatProvider
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
    public BString(string byteString)
        : this(Encoding.UTF8.GetBytes(byteString)) { }

    public string AsString()
    {
        // FIXME(Unavailable): What kind of errors this would return if this
        // contains invalid `utf8`?
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
