namespace Databento.Interop;

/// <summary>
/// Exception thrown when authentication fails (invalid API key or insufficient permissions)
/// </summary>
public class AuthenticationException : DbentoException
{
    public AuthenticationException(string message) : base(message)
    {
    }

    public AuthenticationException(string message, int errorCode) : base(message, errorCode)
    {
    }
}

/// <summary>
/// Exception thrown when a requested resource is not found (dataset, symbol, job, etc.)
/// </summary>
public class NotFoundException : DbentoException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string message, int errorCode) : base(message, errorCode)
    {
    }
}

/// <summary>
/// Exception thrown when rate limit is exceeded
/// </summary>
public class RateLimitException : DbentoException
{
    /// <summary>
    /// Time when the rate limit will reset (if available)
    /// </summary>
    public DateTimeOffset? RetryAfter { get; set; }

    public RateLimitException(string message) : base(message)
    {
    }

    public RateLimitException(string message, int errorCode) : base(message, errorCode)
    {
    }

    public RateLimitException(string message, int errorCode, DateTimeOffset retryAfter)
        : base(message, errorCode)
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Exception thrown when request validation fails (invalid parameters, schema mismatches, etc.)
/// </summary>
public class ValidationException : DbentoException
{
    public ValidationException(string message) : base(message)
    {
    }

    public ValidationException(string message, int errorCode) : base(message, errorCode)
    {
    }
}

/// <summary>
/// Exception thrown when server encounters an internal error
/// </summary>
public class ServerException : DbentoException
{
    public ServerException(string message) : base(message)
    {
    }

    public ServerException(string message, int errorCode) : base(message, errorCode)
    {
    }
}

/// <summary>
/// Exception thrown when a timeout occurs
/// </summary>
public class TimeoutException : DbentoException
{
    public TimeoutException(string message) : base(message)
    {
    }

    public TimeoutException(string message, int errorCode) : base(message, errorCode)
    {
    }
}

/// <summary>
/// Exception thrown when a network connection fails
/// </summary>
public class ConnectionException : DbentoException
{
    public ConnectionException(string message) : base(message)
    {
    }

    public ConnectionException(string message, int errorCode) : base(message, errorCode)
    {
    }

    public ConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
