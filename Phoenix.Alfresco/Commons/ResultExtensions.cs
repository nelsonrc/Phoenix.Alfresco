using System;
using Microsoft.Extensions.Logging;

namespace Phoenix.Alfresco.Commons;

public static class ResultExtensions
{
    /// <summary>
    /// Wraps a value in a successful Result with an optional message.
    /// </summary>
    public static Result<T> AsOkResult<T>(this T value, string? message = null)
    {
        return Result<T>.Ok(value, message ?? "Success.");
    }

    /// <summary>
    /// Wraps a value in a failed Result with a provided failure reason.
    /// </summary>
    public static Result<T> AsFailResult<T>(this T? value, string reason)
    {
        return Result<T>.Fail(reason);
    }

    /// <summary>
    /// Maps a nullable value into a Result—success if not null, fail otherwise.
    /// </summary>
    public static Result<T> AsResult<T>(this T? value, string? failureMessage = null)
    {
        return value is not null
            ? Result<T>.Ok(value, "Success.")
            : Result<T>.Fail(failureMessage ?? "Value was null.");
    }

       public static Result<T> WithLogging<T>(this Result<T> result, Action<string> logError, string context)
    {
        if (!result.Success)
            logError($"[{context}] → {result.Message}");

        return result;
    }

    public static Result<T> WithLogging<T>(this Result<T> result, ILogger logger, LogLevel level, string context)
    {
        if (!result.Success)
            logger.Log(level, "{Context} failed: {Message}", context, result.Message);
        return result;
    }


    public static Result<T> AppendContext<T>(this Result<T> result, string additionalContext)
    {
        if (!result.Success && !string.IsNullOrWhiteSpace(result.Message))
            return Result<T>.Fail($"{additionalContext}: {result.Message}");

        return result;
    }

    public static async Task<Result<T>> FailFromResponseAsync<T>(
        HttpResponseMessage response,
        string operation,
        CancellationToken ct = default)
    {
        var error = await response.Content.ReadAsStringAsync(ct);
        var message = $"[{operation}] failed with status {response.StatusCode} – {error}";
        return Result<T>.Fail(message);
    }
}
