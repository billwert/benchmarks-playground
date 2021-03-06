using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace weatherapp_controllers
{
    [ApiController]
    public class WeatherController : ControllerBase
    {
        private readonly IHttpClientFactory _factory;

        public WeatherController(IHttpClientFactory factory)
        {
            _factory = factory;
        }

        [HttpGet("/")]
        public async Task<ActionResult<WeatherReport>> GetWeather()
        {
            var client = _factory.CreateClient();
            var forecast = await client.GetFromJsonAsync<WeatherForecast>("/forecast");
            return new WeatherReport() { Location = "Seattle", Forecast = forecast.Weather, };
        }
    }
}