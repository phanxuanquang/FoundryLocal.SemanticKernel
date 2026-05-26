using Microsoft.AI.Foundry.Local;
using System.ComponentModel.DataAnnotations;

namespace FoundryLocal.SemanticKernel.Options;

public sealed record FoundryLocalOptions
{
    [Required]
    public string AppName { get; init; }

    [Required]
    public string WebServiceUrl { get; init; }

    [Required]
    public string ModelAliasOrId { get; init; }

    public LogLevel LogLevel { get; init; } = LogLevel.Information;
}