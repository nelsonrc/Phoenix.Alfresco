using System;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;

namespace Phoenix.Helpers;

public static class LogHelpers
{
    public static IDisposable BeginLogScope(
        this ILogger _logger,
        string scopeName,
        object values)
    {
        return _logger.BeginScope("{@ScopeName}:{@ScopeValues}", scopeName, values)!;
    }

    public static IDisposable BeginLogScope(
        this ILogger _logger,
        string scopeName,
        params object[] values)
    {
        return _logger.BeginScope("{@ScopeName}:{@ScopeValues}", scopeName, values)!;
    }

    public static IDisposable BeginLogScope(
        this ILogger _logger,
        string scopeName)
    {
        return _logger.BeginScope("{@ScopeName}", scopeName)!;
    }


    public static void LogWithContext<T>(
        this ILogger _logger, 
        LogLevel level,
        string eventName,
        T context,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} {@Context} [Caller={Caller}]", eventName, context, caller);
    }

    public static void LogWithContext(
        this ILogger _logger, 
        LogLevel level,
        string eventName,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} [Caller={Caller}]", eventName, caller);
    }

    public static void LogWithContext(
        this ILogger _logger, 
        LogLevel level,
        string eventName,
        string message,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, "{EventName} {Message} [Caller={Caller}]", eventName, message, caller);
    }

    public static void LogWithContext(
        this ILogger _logger, 
        LogLevel level,
        string eventName,
        Exception ex,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, ex, "{EventName} [Caller={Caller}]", eventName, caller);
    }

    public static void LogWithContext(
        this ILogger _logger, 
        LogLevel level,
        string eventName,
        string message,
        Exception ex,
        [CallerMemberName] string caller = "")
    {
        _logger.Log(level, ex, "{EventName} {Message} [Caller={Caller}]", eventName, message, caller);
    }

}
