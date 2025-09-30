namespace Core.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);
public sealed class ValidationException(string msg) : DomainException(msg);
public sealed class ConflictException(string msg)   : DomainException(msg);
public sealed class NotFoundException(string msg)   : DomainException(msg);
public sealed class ExpiredException(string msg)    : DomainException(msg);
