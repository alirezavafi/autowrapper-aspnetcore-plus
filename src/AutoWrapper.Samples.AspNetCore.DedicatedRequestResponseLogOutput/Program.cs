using System;
using AutoWrapper.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Compact;
using Serilog.Sinks.SystemConsole.Themes;

namespace AutoWrapper.Samples.AspNetCore.DedicatedRequestResponseLogOutput
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
                        .UseSerilogPlus(l =>
                        {
                            l
                                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                                .MinimumLevel.Override("System", LogEventLevel.Warning)
                                .WriteTo.File(new CompactJsonFormatter(), $"App_Data/Logs/log_default-{DateTime.Now:yyyyMMdd-HHmmss}.json")
                                .WriteTo.Console(
                                    outputTemplate:
                                    "[{Timestamp:HH:mm:ss} {Level:u3}] {Message} {NewLine}{Properties} {NewLine}{Exception}{NewLine}",
                                    theme: SystemConsoleTheme.Literate,
                                    restrictedToMinimumLevel: LogEventLevel.Information);
                        })
                        .UseAutoWrapperPlus(new AutoWrapperRegistrationOptions()
                        {
                            RegisterLoggerAsDefaultLogger = false,
                            LoggerConfiguration = (l) =>
                            {
                                l.SetSerilogPlusDefaultConfiguration()
                                    .WriteTo.File(new RenderedCompactJsonFormatter(),
                                        $"App_Data/Logs/log_autowrapper-{DateTime.Now:yyyyMMdd-HHmmss}.json");
                            },
                            AutoWrapperOptions = new AutoWrapperOptions()
                            {
                                LogMode = LogMode.LogAll,
                                RequestHeaderLogMode = LogMode.LogAll,
                                RequestBodyLogMode = LogMode.LogAll,
                                ResponseHeaderLogMode = LogMode.LogAll,
                                ResponseBodyLogMode = LogMode.LogAll,
                                UseApiProblemDetailsException = true,
                                RequestBodyLogTextLengthLimit = 5000,
                                ResponseBodyLogTextLengthLimit = 5000,
                                MaskFormat = "***",
                                MaskedProperties =
                                    { "*password*", "*token*", "*secret*", "*authorization*", "*client-secret*" },
                            }
                        })
                        .UseStartup<Startup>();
                });
    }
}