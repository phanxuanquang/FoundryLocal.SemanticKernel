using FoundryLocal.SemanticKernel.Implementations;
using Microsoft.Extensions.DependencyInjection;
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
        // Register the inner OpenAI service by its concrete type (not as IChatCompletionService) so the decorator can resolve and wrap it.
        services.AddSingleton(new OpenAIChatCompletionService(
            modelId: modelAlias,
            apiKey: "NO-API-KEY-NEEDED",
            endpoint: endpoint));

        // Register the decorator as the IChatCompletionService the Kernel will resolve. It intercepts Qwen3's XML tool-call text and runs the full agentic loop.
        return services.AddSingleton<IChatCompletionService>(sp => new FoundryLocalChatCompletionService(sp.GetRequiredService<OpenAIChatCompletionService>()));
    }
}
