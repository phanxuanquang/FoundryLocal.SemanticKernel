using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

public sealed class FilePlugin
{
    private readonly ILogger<FilePlugin> _logger;

    public FilePlugin(ILogger<FilePlugin> logger)
    {
        _logger = logger;
    }

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