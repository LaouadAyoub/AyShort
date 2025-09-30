using System.Text.RegularExpressions;
using Core.Domain.Exceptions;

namespace Core.Domain.ValueObjects;

public sealed class ShortCode
{
    public string Value { get; }
    private ShortCode(string value) => Value = value;

    private static readonly Regex Pattern = new("^[a-zA-Z0-9_-]{3,20}$", RegexOptions.Compiled);

    public static ShortCode Create(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) throw new ValidationException("Code is required.");
        if (!Pattern.IsMatch(input)) throw new ValidationException("Code must match [a-zA-Z0-9_-]{3,20}.");
        return new ShortCode(input);
    }

    public override string ToString() => Value;
}
