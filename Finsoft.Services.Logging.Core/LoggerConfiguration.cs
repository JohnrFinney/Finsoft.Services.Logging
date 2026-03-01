namespace Finsoft.Services.Logging.Core;

public class LoggerConfiguration
{
    public bool AllowWriteToEventLog { get; set; } = false;
    public string? SmtpHost { get; set; }
    public string? ErrorEmail { get; set; }
    public string? EmailFrom { get; set; }
    public string? EmailSubjectPrefix { get; set; }
    public int SmtpPort { get; set; } = 25;
    public string? LogFilePath { get; set; }
    public string? ConnectionString { get; set; }
    public string? ApplicationName { get; set; }
}
