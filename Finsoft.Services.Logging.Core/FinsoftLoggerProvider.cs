using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Finsoft.Services.Logging.Core;

/// <summary>
/// An <see cref="ILoggerProvider"/> that creates <see cref="FinsoftLogger"/> instances,
/// routing all standard .NET ILogger calls to the Finsoft SQL logging table.
/// </summary>
public class FinsoftLoggerProvider : ILoggerProvider
{
    private readonly LoggingService _loggingService;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ConcurrentDictionary<string, FinsoftLogger> _loggers = new();

    public FinsoftLoggerProvider(LoggingService loggingService, IHttpContextAccessor? httpContextAccessor)
    {
        _loggingService = loggingService;
        _httpContextAccessor = httpContextAccessor;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return _loggers.GetOrAdd(categoryName, name => new FinsoftLogger(name, _loggingService, _httpContextAccessor));
    }

    public void Dispose()
    {
        _loggers.Clear();
    }
}
