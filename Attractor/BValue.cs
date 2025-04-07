using System.Numerics;
using OneOf;

namespace Attractor;

using BDictionnary = SortedDictionary<BString, BValue>;
using BList = List<BValue>;

/// <summary>
/// A benconded torrent value
/// </summary>
[GenerateOneOf]
public partial class BValue : OneOfBase<BString, BigInteger, BList, BDictionnary>
{
    // PERF(Unavailable): Encourage on the documentation to buffer the stream
    // to optimize performance.

    // FIXME(Unavailable): The `Stream` needs to support seeking (which is fixed
    // by using a BufferedStream).

    // FIXME(Unavailable): Catch all exceptions as `UnexpectedEof`.

    /// <summary>
    /// Parses a <see cref="BValue"/> from <paramref name="reader"/>.
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
        // most numbers would be small
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

        // FIXME(Unavailable): Check `-0`
        // FIXME(Unavailable): Leading zeros
        if (BigInteger.TryParse(chars.ToArray(), out var bint))
        {
            return new BValue(bint);
        }

        return Error.InvalidFormat;
    }

    private static OneOf<BValue, Error> ParseList(BinaryReader reader)
    {
        BList result = [];

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

        return new BValue(result);
    }

    private static OneOf<BValue, Error> ParseDictionnary(BinaryReader reader)
    {
        BDictionnary result = [];

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

            // TODO(Unvailable): The spec indicates that keys should be sorted
            // "as raw strings"; I imagine this is to exploit binary search algs?

            if (Parse(reader).TryPickT0(out var value, out var valueError))
            {
                result.Add(keyString, value);
            }
            else
            {
                return valueError;
            }
        }

        return new BValue(result);
    }
}

// TODO(Unavailable): `IsUTF8` property hint (I think it can be useful for
// BDictionary keys).
public record struct BString(byte[] Bytes)
{
    public readonly string AsString()
    {
        return System.Text.Encoding.UTF8.GetString([.. Bytes]);
    }
}

// errors

[GenerateOneOf]
public partial class Error : OneOfBase<InvalidPrefixChar, InvalidFormat, UnexpectedEof>
{
    public static readonly Error InvalidPrefixChar = new InvalidPrefixChar();
    public static readonly Error InvalidFormat = new InvalidFormat();
    public static readonly Error UnexpectedEof = new UnexpectedEof();
}

// TODO(Unavailable): Char property
public record struct InvalidPrefixChar();

// TODO(Unvailable): Message property:
// - Reason:
//   - Missing colon for string parsing
//   - -0
//   - Leading zeros
//   - Expected string for dictionary key
public record struct InvalidFormat();

public record struct UnexpectedEof();
