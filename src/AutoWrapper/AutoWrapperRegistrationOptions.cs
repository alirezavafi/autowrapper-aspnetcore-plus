using System;
using Serilog;

namespace AutoWrapper
{
    public class AutoWrapperRegistrationOptions
    {
        public bool RegisterLoggerAsDefaultLogger { get; set; } = true;
        public Action<LoggerConfiguration> LoggerConfiguration { get; set; }
        public AutoWrapperOptions AutoWrapperOptions { get; set; }
    }
}