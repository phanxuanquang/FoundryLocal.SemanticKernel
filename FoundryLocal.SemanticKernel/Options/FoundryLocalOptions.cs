using Microsoft.AI.Foundry.Local;

namespace FoundryLocal.SemanticKernel.Options;

public sealed class FoundryLocalOptions
{
    public string AppName { get; set; } = "FoundryLocal-SemanticKernel";

    public string WebServiceUrl { get; set; } = "http://127.0.0.1:52495";

    public string DefaultModelAlias { get; set; } = "phi-4-mini";

    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}