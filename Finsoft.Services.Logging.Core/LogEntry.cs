using System;

namespace Finsoft.Services.Logging.Core;

public class LogEntry
{
    public int Id { get; set; }
    public string? LogLevel { get; set; }
    public string? ApplicationName { get; set; }
    public string? RequestPath { get; set; }
    public string? Message { get; set; }
    public string? Exception { get; set; }
    public string? InnerExceptionMessage { get; set; }
    public string? InnerException { get; set; }
    public string? HttpMethod { get; set; }
    public string? ClientIpAddress { get; set; }
    public string? RequestPacket { get; set; }
    public string? ResponsePacket { get; set; }
    public string? Url { get; set; }
    public int? ResponseTimeMs { get; set; }
    public DateTime DateCreated { get; set; }
}
