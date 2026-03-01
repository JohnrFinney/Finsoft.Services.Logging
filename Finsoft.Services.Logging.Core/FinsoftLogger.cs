using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Finsoft.Services.Logging.Core;

/// <summary>
/// An <see cref="ILogger"/> implementation that bridges to <see cref="LoggingService"/>
/// so that standard .NET ILogger calls are written to the Finsoft SQL logging table.
/// </summary>
public class FinsoftLogger : ILogger
{
    private readonly string _categoryName;
    private readonly LoggingService _loggingService;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public FinsoftLogger(string categoryName, LoggingService loggingService, IHttpContextAccessor? httpContextAccessor)
    {
        _categoryName = categoryName;
        _loggingService = loggingService;
        _httpContextAccessor = httpContextAccessor;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;

        var message = $"[{_categoryName}] {formatter(state, exception)}";
        var finsoftLevel = MapLogLevel(logLevel);
        var httpContext = _httpContextAccessor?.HttpContext;

        // Fire-and-forget: ILogger.Log is synchronous but LoggingService is async.
        // Use Task.Run to avoid blocking the calling thread or risking deadlocks.
        _ = Task.Run(async () =>
        {
            try
            {
                await _loggingService.Log(finsoftLevel, message, exception, httpContext: httpContext);
            }
            catch
            {
                // Never let logging failures crash the application
            }
        });
    }

    private static eLogLevel MapLogLevel(LogLevel logLevel) => logLevel switch
    {
        LogLevel.Trace => eLogLevel.Debug,
        LogLevel.Debug => eLogLevel.Debug,
        LogLevel.Information => eLogLevel.Information,
        LogLevel.Warning => eLogLevel.Warning,
        LogLevel.Error => eLogLevel.Error,
        LogLevel.Critical => eLogLevel.Error,
        _ => eLogLevel.Information
    };
}
