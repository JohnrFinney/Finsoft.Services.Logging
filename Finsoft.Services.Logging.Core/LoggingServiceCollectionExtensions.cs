using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;

namespace Finsoft.Services.Logging.Core
{
    public static class LoggingServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="ILoggingService"/> and <see cref="LoggingService"/> as singletons,
        /// and adds <see cref="FinsoftLoggerProvider"/> so that standard ILogger calls also
        /// write to the Finsoft SQL logging table.
        /// </summary>
        public static IServiceCollection AddFinsoftLogger(this IServiceCollection services, Action<LoggerConfiguration> configure)
        {
            services.Configure(configure);

            // Register the concrete LoggingService as a singleton (needed by the ILoggerProvider)
            services.AddSingleton<LoggingService>(sp =>
            {
                var config = sp.GetRequiredService<IOptions<LoggerConfiguration>>().Value;
                return new LoggingService(config);
            });

            // Expose it via the ILoggingService interface too (for middleware and direct use)
            services.AddSingleton<ILoggingService>(sp => sp.GetRequiredService<LoggingService>());

            // Register the ILoggerProvider so standard ILogger calls flow to SQL
            services.AddSingleton<ILoggerProvider, FinsoftLoggerProvider>(sp =>
            {
                var loggingService = sp.GetRequiredService<LoggingService>();
                var httpContextAccessor = sp.GetService<IHttpContextAccessor>();
                return new FinsoftLoggerProvider(loggingService, httpContextAccessor);
            });

            return services;
        }
    }
}
