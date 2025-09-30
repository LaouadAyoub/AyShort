using System.Security.Cryptography;
using Core.Application.Ports.Out;

namespace Adapters.Out.Persistence.InMemory;

public sealed class Base62CodeGenerator : ICodeGenerator
{
    private const string Alphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public string Generate(int length = 7)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        Span<char> chars = stackalloc char[length];
        for (int i = 0; i < length; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        return new string(chars);
    }
}
