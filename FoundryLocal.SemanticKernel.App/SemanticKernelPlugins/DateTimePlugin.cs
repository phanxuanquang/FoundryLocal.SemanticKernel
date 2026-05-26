using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

[Description("Provides current date and time information.")]
public class DateTimePlugin(ILogger<DateTimePlugin>? logger = null)
{
    private readonly ILogger<DateTimePlugin> _logger = logger ?? new LoggerFactory().CreateLogger<DateTimePlugin>();

    [KernelFunction()]
    [Description("Gets the current local date and time.")]
    public string GetCurrentDateTime()
    {
        _logger.LogInformation("Getting current date and time.");
        return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }
}
