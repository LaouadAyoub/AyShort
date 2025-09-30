using Core.Domain.Exceptions;

namespace Core.Domain.ValueObjects;

public sealed class OriginalUrl
{
    public string Value { get; }

    private OriginalUrl(string value) => Value = value;

    public static OriginalUrl Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ValidationException("URL is required.");
        if (input.Length > 2048) throw new ValidationException("URL too long.");
        if (!Uri.TryCreate(input, UriKind.Absolute, out var uri)) throw new ValidationException("URL must be absolute.");
        if (uri.Scheme is not ("http" or "https")) throw new ValidationException("Only http/https are allowed.");
        return new OriginalUrl(uri.ToString());
    }

    public override string ToString() => Value;
}
