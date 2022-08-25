using System;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Debugging;

namespace AutoWrapper
{
    public static class AutoWrapperExtensions
    {
        public static IWebHostBuilder UseAutoWrapperPlus(this IWebHostBuilder host,
            Action<AutoWrapperRegistrationOptions> optionsBuilder)
        {
            var op = new AutoWrapperRegistrationOptions();
            optionsBuilder?.Invoke(op);
            host.UseAutoWrapperPlus(op);
            return host;
        }

        public static IWebHostBuilder UseAutoWrapperPlus(this IWebHostBuilder host,
            AutoWrapperRegistrationOptions options = default)
        {
            options ??= new AutoWrapperRegistrationOptions();
            var optionsAutoWrapperOptions = options.AutoWrapperOptions ?? new AutoWrapperOptions();
            if (options.RegisterLoggerAsDefaultLogger)
            {
                host.UseSerilogPlus(options.LoggerConfiguration);
            }
            else
            {
                var loggerConfiguration = new LoggerConfiguration();
                options.LoggerConfiguration?.Invoke(loggerConfiguration);
                loggerConfiguration.SetSerilogPlusDefaultConfiguration();
                optionsAutoWrapperOptions.Logger = loggerConfiguration.CreateLogger();
            }
            
            host.ConfigureServices(s =>
            {
                s.AddTransient<IStartupFilter, AutoWrapperStartupFilter>();
                s.AddSingleton(optionsAutoWrapperOptions);
            });
            return host;
        }
        
        public static IWebHostBuilder UseAutoWrapperPlus<T>(this IWebHostBuilder host,
            Action<AutoWrapperRegistrationOptions> optionsBuilder)
        {
            var op = new AutoWrapperRegistrationOptions();
            optionsBuilder?.Invoke(op);
            host.UseAutoWrapperPlus<T>(op);
            return host;
        }

        public static IWebHostBuilder UseAutoWrapperPlus<T>(this IWebHostBuilder host,
            AutoWrapperRegistrationOptions options = default)
        {
            options ??= new AutoWrapperRegistrationOptions();
            var optionsAutoWrapperOptions = options.AutoWrapperOptions ?? new AutoWrapperOptions();
            if (options.RegisterLoggerAsDefaultLogger)
            {
                host.UseSerilogPlus(options.LoggerConfiguration);
            }
            else
            {
                var loggerConfiguration = new LoggerConfiguration();
                options.LoggerConfiguration?.Invoke(loggerConfiguration);
                loggerConfiguration.SetSerilogPlusDefaultConfiguration();
                optionsAutoWrapperOptions.Logger = loggerConfiguration.CreateLogger();
            }
            
            host.ConfigureServices(s =>
            {
                s.AddTransient<IStartupFilter, AutoWrapperStartupFilter<T>>();
                s.AddSingleton(optionsAutoWrapperOptions);
            });
            return host;
        }

    }
}