using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;


[Description("Provides weather information for cities.")]
public sealed class WeatherPlugin
{
    private readonly ILogger<WeatherPlugin> _logger;
    public WeatherPlugin(ILogger<WeatherPlugin>? logger = null)
    {
        _logger = logger ?? new LoggerFactory().CreateLogger<WeatherPlugin>();
    }

    private static readonly Dictionary<string, (string Condition, int TempC, int Humidity)> MockWeatherData = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Seattle"] = ("Cloudy with light rain", 14, 82),
        ["New York"] = ("Partly cloudy", 22, 55),
        ["London"] = ("Overcast", 16, 75),
        ["Tokyo"] = ("Sunny", 28, 40),
        ["Paris"] = ("Clear skies", 20, 50),
        ["Sydney"] = ("Sunny and warm", 25, 45),
        ["San Francisco"] = ("Foggy", 17, 78),
        ["Berlin"] = ("Light clouds", 18, 60),
        ["Mumbai"] = ("Hot and humid", 34, 85),
        ["Toronto"] = ("Snow flurries", 1, 70),
    };

    [KernelFunction("get_weather")]
    [Description("Gets the current weather for a specified city. Returns temperature, conditions, and humidity.")]
    public string GetWeather([Description("The city name to get weather for")] string city)
    {
        if (MockWeatherData.TryGetValue(city, out var weather))
        {
            return $"Weather in {city}: {weather.Condition}, Temperature: {weather.TempC}°C, Humidity: {weather.Humidity}%";
        }

        return $"Weather in {city}: Partly cloudy, Temperature: 20°C, Humidity: 55% (default/estimated)";
    }

    [KernelFunction("get_forecast")]
    [Description("Gets a weather forecast for a specified city for a given number of days ahead.")]
    public string GetForecast(
        [Description("The city name to get the forecast for")] string city,
        [Description("Number of days ahead to forecast (1-7)")] int days)
    {
        days = Math.Clamp(days, 1, 7);
        var baseTemp = MockWeatherData.TryGetValue(city, out var weather) ? weather.TempC : 20;
        var random = new Random(city.GetHashCode() + days);

        var forecasts = new List<string>();
        for (int i = 1; i <= days; i++)
        {
            var date = DateTime.Now.AddDays(i).ToString("MM/dd");
            var temp = baseTemp + random.Next(-5, 6);
            var conditions = new[] { "Sunny", "Partly cloudy", "Cloudy", "Light rain", "Clear" };
            var condition = conditions[random.Next(conditions.Length)];
            forecasts.Add($"  {date}: {condition}, {temp}°C");
        }

        return $"Forecast for {city} ({days} days):\n{string.Join("\n", forecasts)}";
    }
}