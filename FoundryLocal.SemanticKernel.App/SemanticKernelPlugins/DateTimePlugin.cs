using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

[Description("Provides current date and time information.")]
public class DateTimePlugin
{
    private readonly ILogger<DateTimePlugin> _logger;
    public DateTimePlugin(ILogger<DateTimePlugin>? logger = null)
    {
        // Use console logging if no logger is provided, to ensure we have some visibility into plugin operations without requiring DI setup.
        _logger = logger ?? new LoggerFactory().CreateLogger<DateTimePlugin>();
    }

    [KernelFunction("get_current_time")]
    [Description("Gets the current local time.")]
    public string GetCurrentTime()
    {
        _logger.LogInformation("Getting current time.");
        return DateTime.Now.ToString("HH:mm:ss");
    }

    [KernelFunction("get_current_date")]
    [Description("Gets the current local date.")]
    public string GetCurrentDate()
    {
        _logger.LogInformation("Getting current date.");
        return DateTime.Now.ToString("yyyy-MM-dd");
    }

    [KernelFunction("get_current_datetime")]
    [Description("Gets the current local date and time.")]
    public string GetCurrentDateTime()
    {
        _logger.LogInformation("Getting current date and time.");
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    [KernelFunction("get_day_of_week")]
    [Description("Gets the current day of the week.")]
    public string GetDayOfWeek()
    {
        _logger.LogInformation("Getting current day of the week.");
        return DateTime.Now.DayOfWeek.ToString();
    }
}
