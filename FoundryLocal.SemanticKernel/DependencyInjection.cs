using FoundryLocal.SemanticKernel.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FoundryLocal.SemanticKernel;

#pragma warning disable SKEXP0010
public static class DependencyInjection
{
    /// <summary>
    /// Adds the FoundryLocalChatCompletionService as a decorator around the OpenAIChatCompletionService.
    /// </summary>
    /// <param name="services"></param>
    /// <param name="modelAlias">The model alias to use for the chat completion service (e.g., "phi-4-mini").</param>
    /// <param name="endpoint">The local endpoint URI for the chat completion service (e.g., "http://127.0.0.1:52495").</param>
    /// <returns></returns>
    public static IServiceCollection AddFoundryLocalChatCompletion(this IServiceCollection services, string modelAlias, Uri endpoint)
    {
        // Register the decorator as the IChatCompletionService the Kernel will resolve. 
        return services.AddSingleton<IChatCompletionService>(sp => new FoundryLocalChatCompletionService(
            inner: new OpenAIChatCompletionService(modelId: modelAlias, endpoint: endpoint),
            logger: sp.GetService<ILogger<FoundryLocalChatCompletionService>>()));
    }
}
