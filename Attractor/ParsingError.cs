#pragma warning disable CS8851

using OneOf;

namespace Attractor;

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

    // NOTE(Unavailable): Try to order these by the 'category' of the helper.

    internal static readonly ParsingError EndOfStream = new EndOfStreamException();

    internal static ParsingError FormatException(string message)
    {
        return new FormatException(message);
    }

    internal static ParsingError FormatException(string message, Exception exception)
    {
        return new FormatException(message, exception);
    }

    internal static ParsingError FormatException(string message, ParsingError error)
    {
        return new FormatException(message, error.AsException());
    }

    // NOTE(Unavailable): Fixes ambiguous calls with
    // `FormatException(string, ParsingError)`.
    internal static ParsingError FormatException(string message, FormatException error)
    {
        return new FormatException(message, error);
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
