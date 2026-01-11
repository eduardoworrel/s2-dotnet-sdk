using System.Net;

namespace S2.StreamStore.Exceptions;

/// <summary>
/// Base exception for all S2 errors.
/// </summary>
public class S2Exception : Exception
{
    /// <summary>
    /// HTTP status code if this was an HTTP error.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// Error code from S2 API response.
    /// </summary>
    public string? ErrorCode { get; }

    public S2Exception(string message) : base(message) { }

    public S2Exception(string message, Exception innerException)
        : base(message, innerException) { }

    public S2Exception(string message, HttpStatusCode statusCode, string? errorCode = null)
        : base(message)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }
}

/// <summary>
/// Thrown when a stream is not found.
/// </summary>
public class StreamNotFoundException : S2Exception
{
    public string StreamName { get; }

    public StreamNotFoundException(string streamName)
        : base($"Stream '{streamName}' not found", HttpStatusCode.NotFound, "STREAM_NOT_FOUND")
    {
        StreamName = streamName;
    }
}

/// <summary>
/// Thrown when a basin is not found.
/// </summary>
public class BasinNotFoundException : S2Exception
{
    public string BasinName { get; }

    public BasinNotFoundException(string basinName)
        : base($"Basin '{basinName}' not found", HttpStatusCode.NotFound, "BASIN_NOT_FOUND")
    {
        BasinName = basinName;
    }
}

/// <summary>
/// Thrown when the request is rate limited.
/// </summary>
public class RateLimitedException : S2Exception
{
    /// <summary>
    /// Time to wait before retrying.
    /// </summary>
    public TimeSpan? RetryAfter { get; }

    public RateLimitedException(TimeSpan? retryAfter = null)
        : base("Rate limit exceeded", HttpStatusCode.TooManyRequests, "RATE_LIMITED")
    {
        RetryAfter = retryAfter;
    }
}

/// <summary>
/// Thrown when authentication fails.
/// </summary>
public class AuthenticationException : S2Exception
{
    public AuthenticationException(string message = "Authentication failed")
        : base(message, HttpStatusCode.Unauthorized, "UNAUTHORIZED") { }
}

/// <summary>
/// Thrown when a stream or basin already exists.
/// </summary>
public class AlreadyExistsException : S2Exception
{
    public AlreadyExistsException(string message)
        : base(message, HttpStatusCode.Conflict, "ALREADY_EXISTS") { }
}
