namespace Core.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class ValidationException(string msg) : DomainException(msg);
public sealed class ConflictException : DomainException
{
    public ConflictException(string msg) : base(msg) { }
    public ConflictException(string msg, Exception innerException) : base(msg, innerException) { }
}
public sealed class NotFoundException(string msg) : DomainException(msg);
public sealed class ExpiredException(string msg) : DomainException(msg);
public sealed class InfrastructureException : DomainException
{
    public InfrastructureException(string msg) : base(msg) { }
    public InfrastructureException(string msg, Exception innerException) : base(msg, innerException) { }
}
