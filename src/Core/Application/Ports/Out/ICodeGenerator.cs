namespace Core.Application.Ports.Out;

public interface ICodeGenerator
{
    string Generate(int length = 7);
}
