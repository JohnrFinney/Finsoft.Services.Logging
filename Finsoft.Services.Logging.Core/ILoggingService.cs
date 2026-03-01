using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Finsoft.Services.Logging.Core;

public interface ILoggingService
{
    Task LogInformation(string message, HttpContext? httpContext = null);
    Task LogWarning(string message, HttpContext? httpContext = null);
    Task LogError(string message, HttpContext? httpContext = null);
    Task LogError(Exception exception, string message, HttpContext? httpContext = null);
    Task Log(
        eLogLevel? logLevel,
        string? message,
        Exception? exception = null,
        string? requestPacket = null,
        string? responsePacket = null,
        HttpContext? httpContext = null,
        int? responseTimeMs = null);
    void WriteErrorToFile(Exception exception, string className, string section);
    void WriteErrorToFile(string error, string className, string section);
    void EmailError(string errorMessage);
}
