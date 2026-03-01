# Finsoft Services Logging Core

A .NET 10 class library for logging to SQL Server with optional email notifications. Designed to be published as a NuGet package and used in web or background services.

## Features
- Write log entries to a SQL Server table (dbo.Logging)
- Capture exception details (message, inner exception, stack)
- Enrich logs from HttpContext (method, path, URL) when available
- Derive client IP (X-Forwarded-For, RemoteIpAddress, or machine fallback)
- Send error emails using SMTP
- Simple file-based fallback for error messages

## Install and Configure
Add and configure via DI:

```csharp
using Finsoft.Logging.Core;
using Microsoft.Extensions.DependencyInjection;

services.AddFinsoftLogger(cfg =>
{
    cfg.ConnectionString = "<your-connection-string>";
    cfg.ApplicationName = "MyApp";
    // Optional SMTP
    cfg.SmtpHost = "smtp.example.com";
    cfg.ErrorEmail = "errors@example.com";
    cfg.EmailFrom = "no-reply@example.com";
    cfg.SmtpPort = 25;
});
```

Resolve and log:

```csharp
var logger = provider.GetRequiredService<LoggingService>();
await logger.Log("Info", "Hello world");
```

With HttpContext enrichment:

```csharp
await logger.Log(
    logLevel: "Error",
    message: "Something failed",
    exception: new Exception("Boom"),
    requestPath: null,                  // optional override
    requestPacket: "{ request }",
    responsePacket: "{ response }",
    httpContext: httpContext            // from middleware/controller
);
```

## Expected Table Schema
The INSERT targets dbo.Logging with columns:
- LogLevel, ApplicationName, RequestPath, Message
- Exception, InnerExceptionMessage, InnerException
- HttpMethod, ClientIpAddress, RequestPacket, ResponsePacket, Url
- DateCreated

Adjust LoggingService if your schema differs.

## API Surface
- LoggingService implements ILoggingService
  - LogInformation/LogWarning/LogError
  - Log(string? logLevel, string? message, Exception? exception = null, string? requestPath = null, string? requestPacket = null, string? responsePacket = null, HttpContext? httpContext = null)
  - WriteErrorToFile/EmailError helpers

## Test Project
The solution includes Finsoft.Logging.Tests.Core with smoke/integration tests that call a real SQL Server.
- Replace the connection string or inject via environment variables for CI
- Tests demonstrate logging with and without HttpContext, and SMTP validation behavior

CI note: Ensure the agent can reach SQL Server and secrets are stored securely. Consider running these tests conditionally per environment.

## Notes
- Target framework: .NET 8
- SqlClient performance counters are disabled by default to avoid initialization issues
- Consider adding retries/telemetry in production