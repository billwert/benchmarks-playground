using System;
using System.Text.Json.Serialization;

namespace weatherapp_controllers
{
    public class WeatherReport
    {
        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("forecast")]
        public string Forecast { get; set; }
    }
}
