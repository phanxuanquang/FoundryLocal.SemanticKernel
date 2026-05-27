using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

public sealed class FilePlugin(ILogger<FilePlugin> logger)
{
    private readonly ILogger<FilePlugin> _logger = logger;

    [KernelFunction("read_text_file")]
    [Description("Read all text content from a file")]
    public async Task<string> ReadTextFileAsync(
        [Description("Full file path")] string path)
    {
        _logger.LogInformation("Reading file: {Path}", path);

        if (!File.Exists(path))
        {
            _logger.LogWarning("File not found: {Path}", path);

            return "File not found";
        }

        return await File.ReadAllTextAsync(path);
    }
}