using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

public sealed class DateTimePlugin(ILogger<DateTimePlugin> logger)
{
    private readonly ILogger<DateTimePlugin> _logger = logger;

    [KernelFunction("get_current_datetime")]
    [Description("Get current local date and time")]
    public string GetCurrentDateTime()
    {
        var now = DateTime.Now;

        _logger.LogInformation("Returning current datetime: {Now}", now);

        return now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
