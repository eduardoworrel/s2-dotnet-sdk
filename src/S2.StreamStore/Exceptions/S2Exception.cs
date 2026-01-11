using System.Net;

namespace S2.StreamStore.Exceptions;

/// <summary>
/// Origin of the error: server (HTTP response) or sdk (local).
/// </summary>
public enum S2ErrorOrigin
{
    /// <summary>Error originated from the S2 server response.</summary>
    Server,
    /// <summary>Error originated from the SDK (local).</summary>
    Sdk
}

/// <summary>
/// Base exception for all S2 errors.
/// Rich error type used by the SDK to surface HTTP and protocol errors.
/// </summary>
public class S2Exception : Exception
{
    /// <summary>
    /// HTTP status code. 0 for non-HTTP/internal errors.
    /// </summary>
    public int Status { get; }

    /// <summary>
    /// HTTP status code if this was an HTTP error.
    /// </summary>
    public HttpStatusCode? StatusCode => Status > 0 ? (HttpStatusCode)Status : null;

    /// <summary>
    /// Error code from S2 API response.
    /// </summary>
    public string? Code { get; }

    /// <summary>
    /// Optional structured error details for diagnostics.
    /// </summary>
    public new object? Data { get; }

    /// <summary>
    /// Origin of the error: server (HTTP response) or sdk (local).
    /// </summary>
    public S2ErrorOrigin Origin { get; }

    public S2Exception(string message, S2ErrorOrigin origin = S2ErrorOrigin.Sdk)
        : base(message)
    {
        Origin = origin;
    }

    public S2Exception(string message, Exception innerException, S2ErrorOrigin origin = S2ErrorOrigin.Sdk)
        : base(message, innerException)
    {
        Origin = origin;
    }

    public S2Exception(
        string message,
        int status,
        string? code = null,
        object? data = null,
        S2ErrorOrigin origin = S2ErrorOrigin.Server)
        : base(message)
    {
        Status = status;
        Code = code;
        Data = data;
        Origin = origin;
    }

    public S2Exception(string message, HttpStatusCode statusCode, string? errorCode = null)
        : base(message)
    {
        Status = (int)statusCode;
        Code = errorCode;
        Origin = S2ErrorOrigin.Server;
    }

    /// <summary>
    /// Returns true if this error is retryable (transient network/server errors).
    /// </summary>
    public bool IsRetryable => Status >= 500 || Status == 429 || Status == 502 || Status == 503 || Status == 504;
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

/// <summary>
/// Thrown when an append operation fails due to a sequence number mismatch.
/// This occurs when you specify a matchSeqNum condition in your append request,
/// but the current tail sequence number of the stream doesn't match.
/// </summary>
public class SeqNumMismatchException : S2Exception
{
    /// <summary>
    /// The expected next sequence number for the stream.
    /// </summary>
    public long ExpectedSeqNum { get; }

    public SeqNumMismatchException(long expectedSeqNum, string? message = null)
        : base(
            message ?? $"Append condition failed: sequence number mismatch. Expected sequence number: {expectedSeqNum}",
            412,
            "APPEND_CONDITION_FAILED",
            origin: S2ErrorOrigin.Server)
    {
        ExpectedSeqNum = expectedSeqNum;
    }
}

/// <summary>
/// Thrown when an append operation fails due to a fencing token mismatch.
/// This occurs when you specify a fencingToken condition in your append request,
/// but the current fencing token of the stream doesn't match.
/// </summary>
public class FencingTokenMismatchException : S2Exception
{
    /// <summary>
    /// The expected fencing token for the stream.
    /// </summary>
    public string ExpectedFencingToken { get; }

    public FencingTokenMismatchException(string expectedFencingToken, string? message = null)
        : base(
            message ?? $"Append condition failed: fencing token mismatch. Expected fencing token: {expectedFencingToken}",
            412,
            "APPEND_CONDITION_FAILED",
            origin: S2ErrorOrigin.Server)
    {
        ExpectedFencingToken = expectedFencingToken;
    }
}

/// <summary>
/// Thrown when a read operation fails because the requested position is beyond the stream tail.
/// This occurs when you specify a startSeqNum that is greater than the current tail
/// of the stream (HTTP 416 Range Not Satisfiable).
/// </summary>
public class RangeNotSatisfiableException : S2Exception
{
    /// <summary>
    /// The current tail sequence number of the stream.
    /// </summary>
    public long? TailSeqNum { get; }

    /// <summary>
    /// The current tail timestamp of the stream.
    /// </summary>
    public long? TailTimestamp { get; }

    public RangeNotSatisfiableException(long? tailSeqNum = null, long? tailTimestamp = null)
        : base(
            tailSeqNum.HasValue
                ? $"Range not satisfiable: requested position is beyond the stream tail (seq_num={tailSeqNum}). Use 'clamp: true' to start from the tail instead."
                : "Range not satisfiable: requested position is beyond the stream tail. Use 'clamp: true' to start from the tail instead.",
            416,
            "RANGE_NOT_SATISFIABLE",
            origin: S2ErrorOrigin.Server)
    {
        TailSeqNum = tailSeqNum;
        TailTimestamp = tailTimestamp;
    }
}

/// <summary>
/// Thrown when a request is cancelled/aborted.
/// </summary>
public class AbortedException : S2Exception
{
    public AbortedException(string message = "Request cancelled")
        : base(message, 499, "ABORTED", origin: S2ErrorOrigin.Sdk) { }
}

/// <summary>
/// Thrown when a connection error occurs (network-level failures).
/// </summary>
public class ConnectionException : S2Exception
{
    public ConnectionException(string message, string? connectionErrorCode = null)
        : base(message, 502, connectionErrorCode ?? "NETWORK_ERROR", origin: S2ErrorOrigin.Sdk) { }
}

/// <summary>
/// Thrown for internal SDK errors (invariant violations).
/// </summary>
public class InternalSdkException : S2Exception
{
    public InternalSdkException(string message, object? details = null)
        : base($"Internal SDK error: {message}", 0, "INTERNAL_SDK_ERROR", details, S2ErrorOrigin.Sdk) { }
}
