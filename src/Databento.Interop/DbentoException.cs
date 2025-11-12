namespace Databento.Interop;

/// <summary>
/// Exception thrown when a Databento operation fails
/// </summary>
public class DbentoException : Exception
{
    public int? ErrorCode { get; }

    public DbentoException(string message) : base(message)
    {
    }

    public DbentoException(string message, int errorCode) : base(message)
    {
        ErrorCode = errorCode;
    }

    public DbentoException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// MEDIUM FIX: Factory method to create specific exception types based on error codes
    /// Maps error codes to appropriate exception types for better error handling
    /// </summary>
    /// <param name="message">Error message</param>
    /// <param name="errorCode">Error code from native layer</param>
    /// <returns>Specific exception instance based on error code</returns>
    public static DbentoException CreateFromErrorCode(string message, int errorCode)
    {
        // Map error codes to specific exception types
        // Using HTTP-style status codes as convention
        return errorCode switch
        {
            // Authentication errors (401, 403)
            401 or 403 => new AuthenticationException(message, errorCode),

            // Not found errors (404)
            404 => new NotFoundException(message, errorCode),

            // Validation errors (400, 422)
            400 or 422 => new ValidationException(message, errorCode),

            // Rate limit errors (429)
            429 => new RateLimitException(message, errorCode),

            // Timeout errors (408, 504)
            408 or 504 => new TimeoutException(message, errorCode),

            // Server errors (500-599)
            >= 500 and < 600 => new ServerException(message, errorCode),

            // Connection errors (negative codes often indicate connection issues)
            < 0 => new ConnectionException(message, errorCode),

            // Default to base DbentoException for unknown codes
            _ => new DbentoException(message, errorCode)
        };
    }
}
