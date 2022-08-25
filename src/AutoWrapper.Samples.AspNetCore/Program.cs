using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoWrapper.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace AutoWrapper.Sample.AspNetCore
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .UseAutoWrapperPlus(new AutoWrapperRegistrationOptions()
                        {
                            RegisterLoggerAsDefaultLogger = true,
                            LoggerConfiguration = ConfigureLogger,
                            AutoWrapperOptions = new AutoWrapperOptions()
                            {
                                LogMode = LogMode.LogAll,
                                RequestHeaderLogMode = LogMode.LogAll,
                                RequestBodyLogMode = LogMode.LogAll,
                                ResponseHeaderLogMode = LogMode.LogAll,
                                ResponseBodyLogMode = LogMode.LogAll,
                                UseApiProblemDetailsException = true
                            }
                        })
                        .UseStartup<Startup>();
                });

        private static void ConfigureLogger(LoggerConfiguration logConfig)
        {
            logConfig
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .WriteTo.File(new CompactJsonFormatter(), $"App_Data/Logs/log-{DateTime.Now:yyyyMMdd-HHmmss}.json")
                .WriteTo.Console(
                    outputTemplate:
                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} {NewLine}{Properties} {NewLine}{Exception}{NewLine}",
                    theme: SystemConsoleTheme.Literate,
                    restrictedToMinimumLevel: LogEventLevel.Information);

        }
    }
}