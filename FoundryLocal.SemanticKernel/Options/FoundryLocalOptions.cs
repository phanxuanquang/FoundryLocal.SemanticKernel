using Microsoft.AI.Foundry.Local;
using System.ComponentModel.DataAnnotations;

namespace FoundryLocal.SemanticKernel.Options;

public sealed record FoundryLocalOptions
{
    [Required]
    public string AppName { get; init; } = default!;

    [Required]
    public string WebServiceUrl { get; init; } = default!;

    [Required]
    public string ModelAlias { get; init; } = default!;

    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}