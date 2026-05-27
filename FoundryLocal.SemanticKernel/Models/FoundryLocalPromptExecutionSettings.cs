using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FoundryLocal.SemanticKernel.Models;

public class FoundryLocalPromptExecutionSettings : OpenAIPromptExecutionSettings
{
    public FoundryLocalPromptExecutionSettings(PromptExecutionSettings? promptExecutionSettings = null) : base()
    {
        if (promptExecutionSettings != null)
        {
            var oai = OpenAIPromptExecutionSettings.FromExecutionSettings(promptExecutionSettings);

            Temperature = oai.Temperature;
            TopP = oai.TopP;
            MaxTokens = oai.MaxTokens;
            StopSequences = oai.StopSequences;
            FrequencyPenalty = oai.FrequencyPenalty;
            PresencePenalty = oai.PresencePenalty;
        }

        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false);
    }

    public static FoundryLocalPromptExecutionSettings FromExecutionSettings(PromptExecutionSettings promptExecutionSettings)
    {
        ArgumentNullException.ThrowIfNull(promptExecutionSettings);

        return new FoundryLocalPromptExecutionSettings(promptExecutionSettings);
    }
}