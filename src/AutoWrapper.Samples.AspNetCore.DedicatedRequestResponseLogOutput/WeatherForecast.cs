using System;

namespace AutoWrapper.Samples.AspNetCore.DedicatedRequestResponseLogOutput
{
    public class WeatherForecast
    {
        public DateTime Date { get; set; }

        public int TemperatureC { get; set; }

        public int TemperatureF => 32 + (int) (TemperatureC / 0.5556);
        public string Token = "x1241432xfgdsfk$!~!@$@%$#^$";
        public string Summary { get; set; }
    }
}