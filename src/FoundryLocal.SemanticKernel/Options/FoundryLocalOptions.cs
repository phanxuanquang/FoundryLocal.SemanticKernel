using Microsoft.AI.Foundry.Local;
using System.ComponentModel.DataAnnotations;

namespace FoundryLocal.SemanticKernel.Options;

public sealed record FoundryLocalOptions
{
    public string AppName { get; init; } = default!;

    [Required]
    public string WebServiceUrl { get; init; } = default!;

    /// <summary>
    /// The alias of the model to use from the Foundry Local catalog. 
    /// Refer to <seealso href="https://www.foundrylocal.ai/models"/> for models details.
    /// Defaults to "qwen3.5-0.8b" which is a smaller model that can run on a wider range of hardware configurations, including those without high-end GPUs.
    /// </summary>
    public string ModelAlias { get; init; } = "qwen3.5-0.8b";

    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}