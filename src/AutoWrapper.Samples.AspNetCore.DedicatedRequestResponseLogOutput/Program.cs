using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Compact;

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
                .ConfigureWebHostDefaults(webBuilder => { webBuilder.UseSerilogPlus(ConfigureLogger).UseStartup<Startup>(); });

        private static void ConfigureLogger(LoggerConfiguration logConfig)
        {
            logConfig.WriteTo.File(new CompactJsonFormatter(), "App_Data/Logs/log_default.json");
        }
    }
}