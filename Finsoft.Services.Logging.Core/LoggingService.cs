using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using System;
using System.Data;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Finsoft.Services.Logging.Core
{
    public class LoggingService : ILoggingService
    {
        private readonly string _connectionString;
        private readonly string _applicationName;
        private readonly LoggerConfiguration _config;

        static LoggingService()
        {
            Environment.SetEnvironmentVariable("DOTNET_SYSTEM_SQLPERFORMANCECOUNTERS_ENABLED", "false");
        }

        public LoggingService(LoggerConfiguration config)
        {
            _config = config ?? new LoggerConfiguration();
            _connectionString = _config.ConnectionString ?? throw new ArgumentNullException(nameof(_config.ConnectionString));
            _applicationName = _config.ApplicationName ?? throw new ArgumentNullException(nameof(_config.ApplicationName));
        }

        public async Task LogInformation(string message, HttpContext? httpContext = null)
        {
            await Log(eLogLevel.Information, message, httpContext: httpContext);
        }

        public async Task LogWarning(string message, HttpContext? httpContext = null)
        {
            await Log(eLogLevel.Warning, message, httpContext: httpContext);
        }

        public async Task LogError(string message, HttpContext? httpContext = null)
        {
            await Log(eLogLevel.Error, message, httpContext: httpContext);
        }

        public async Task LogError(Exception exception, string message, HttpContext? httpContext = null)
        {
            await Log(eLogLevel.Error, message, exception, httpContext: httpContext);
        }

        public async Task Log(
            eLogLevel? logLevel,
            string? message,
            Exception? exception = null,
            string? requestPacket = null,
            string? responsePacket = null,
            HttpContext? httpContext = null,
            int? responseTimeMs = null)
        {
            string? httpMethod = httpContext?.Request?.Method;
            string? computedRequestPath = httpContext?.Request?.Path.Value;

            string? url = null;
            if (httpContext?.Request != null)
            {
                var req = httpContext.Request;
                url = $"{req.Scheme}://{req.Host}{req.Path}{req.QueryString}";
            }

            string? computedClientIp = ResolveClientIp(httpContext);

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = conn.CreateCommand();

            cmd.CommandText = @"
                INSERT INTO [dbo].[Logging]
                ([LogLevel], [ApplicationName], [RequestPath], [Message], [Exception],
                 [InnerExceptionMessage], [InnerException], [HttpMethod], [ClientIpAddress],
                 [RequestPacket], [ResponsePacket], [Url], [ResponseTimeMs], [DateCreated])
                VALUES
                (@LogLevel, @ApplicationName, @RequestPath, @Message, @Exception,
                 @InnerExceptionMessage, @InnerException, @HttpMethod, @ClientIpAddress,
                 @RequestPacket, @ResponsePacket, @Url, @ResponseTimeMs, @DateCreated)";

            cmd.Parameters.Add(new SqlParameter("@LogLevel", SqlDbType.NVarChar, 50) { Value = (object?)logLevel?.ToString() ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@ApplicationName", SqlDbType.NVarChar, 256) { Value = (object?)_applicationName ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@RequestPath", SqlDbType.NVarChar, 2048) { Value = (object?)computedRequestPath ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Message", SqlDbType.NVarChar, -1) { Value = (object?)message ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Exception", SqlDbType.NVarChar, -1) { Value = (object?)exception?.ToString() ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@InnerExceptionMessage", SqlDbType.NVarChar, -1) { Value = (object?)exception?.InnerException?.Message ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@InnerException", SqlDbType.NVarChar, -1) { Value = (object?)exception?.InnerException?.ToString() ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@HttpMethod", SqlDbType.NVarChar, 10) { Value = (object?)httpMethod ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@ClientIpAddress", SqlDbType.NVarChar, 45) { Value = (object?)computedClientIp ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@RequestPacket", SqlDbType.NVarChar, -1) { Value = (object?)requestPacket ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@ResponsePacket", SqlDbType.NVarChar, -1) { Value = (object?)responsePacket ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@Url", SqlDbType.NVarChar, 2048) { Value = (object?)url ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@ResponseTimeMs", SqlDbType.Int) { Value = (object?)responseTimeMs ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@DateCreated", SqlDbType.DateTime2) { Value = DateTime.UtcNow });

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public void WriteErrorToFile(Exception exception, string className, string section)
        {
            WriteErrorToFile(exception.ToString(), className, section);
        }

        public void WriteErrorToFile(string error, string className, string section)
        {
            var dirPath = !string.IsNullOrWhiteSpace(_config.LogFilePath)
                ? _config.LogFilePath
                : Path.Combine(AppContext.BaseDirectory, "Logs");

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            var filePath = Path.Combine(dirPath, "AppError.txt");
            using var writer = new StreamWriter(filePath, append: true);
            writer.WriteLine($"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}] {_applicationName}.{className}.{section} - {error}");
        }

        public void EmailError(string errorMessage)
        {
            if (string.IsNullOrEmpty(_config.SmtpHost) || string.IsNullOrEmpty(_config.ErrorEmail) || string.IsNullOrEmpty(_config.EmailFrom))
            {
                throw new InvalidOperationException("SMTP configuration is required for email functionality. Please provide SmtpHost, ErrorEmail, and EmailFrom in LoggerConfiguration.");
            }

            var subjectPrefix = !string.IsNullOrWhiteSpace(_config.EmailSubjectPrefix)
                ? _config.EmailSubjectPrefix
                : _applicationName;

            using var smtpClient = new SmtpClient(_config.SmtpHost, _config.SmtpPort);
            using var msg = new MailMessage();

            msg.To.Add(new MailAddress(_config.ErrorEmail));
            msg.Subject = $"{subjectPrefix} - Error";
            msg.From = new MailAddress(_config.EmailFrom);
            msg.Body = $"An error has occurred:\n\n{errorMessage}";

            if (!string.IsNullOrEmpty(_config.LogFilePath) && File.Exists(Path.Combine(_config.LogFilePath, "AppError.txt")))
            {
                var attachment = new Attachment(Path.Combine(_config.LogFilePath, "AppError.txt"));
                msg.Attachments.Add(attachment);
            }

            smtpClient.Send(msg);
        }

        private static string? ResolveClientIp(HttpContext? httpContext)
        {
            if (httpContext == null) return null;

            var xff = httpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (!string.IsNullOrWhiteSpace(xff))
            {
                var first = xff.Split(',')[0].Trim();
                if (!string.IsNullOrWhiteSpace(first) && first != "::1")
                    return first;
            }

            var remoteAddress = httpContext.Connection.RemoteIpAddress;
            if (remoteAddress != null && !IPAddress.IPv6Loopback.Equals(remoteAddress))
                return remoteAddress.ToString();

            // ::1 or null — resolve the machine's local IPv4 address
            try
            {
                foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                        return ip.ToString();
                }
            }
            catch
            {
                // Ignore DNS resolution failures
            }

            // Last resort: return 127.0.0.1 rather than ::1
            return "127.0.0.1";

            return null;
        }
    }
}
