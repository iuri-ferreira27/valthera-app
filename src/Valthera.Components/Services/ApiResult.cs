namespace Valthera.Components.Services;

/// <summary>Outcome category of an API call.</summary>
public enum ApiStatus
{
    /// <summary>The call succeeded and a value is available.</summary>
    Ok,

    /// <summary>The API answered 404.</summary>
    NotFound,

    /// <summary>The call failed (API unreachable, timeout, invalid answer, server error).</summary>
    Error,
}

/// <summary>
/// Result of an API call. Failures are values with a friendly message, so screens never need to catch exceptions.
/// </summary>
/// <typeparam name="T">Type of the successful value.</typeparam>
public sealed record ApiResult<T>(ApiStatus Status, T? Value, string? Message)
{
    /// <summary>Gets a value indicating whether the call succeeded.</summary>
    public bool IsSuccess => Status == ApiStatus.Ok;

    /// <summary>Gets a value indicating whether the API answered 404.</summary>
    public bool IsNotFound => Status == ApiStatus.NotFound;

    /// <summary>Creates a successful result.</summary>
    public static ApiResult<T> Ok(T value) => new(ApiStatus.Ok, value, null);

    /// <summary>Creates a not-found result.</summary>
    public static ApiResult<T> NotFound(string message) => new(ApiStatus.NotFound, default, message);

    /// <summary>Creates a failed result with a user-facing message.</summary>
    public static ApiResult<T> Error(string message) => new(ApiStatus.Error, default, message);
}
