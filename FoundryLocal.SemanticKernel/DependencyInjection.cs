using FoundryLocal.SemanticKernel.Implementations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FoundryLocal.SemanticKernel;

#pragma warning disable SKEXP0010
public static class DependencyInjection
{
    public static IServiceCollection AddFoundryLocalChatCompletion(this IServiceCollection services, string modelId, Uri endpoint)
    {
        // Register the inner OpenAI service by its concrete type (not as IChatCompletionService) so the decorator can resolve and wrap it.
        services.AddSingleton(new OpenAIChatCompletionService(
            modelId: modelId,
            apiKey: "NO-API-KEY-NEEDED",
            endpoint: endpoint));

        // Register the decorator as the IChatCompletionService the Kernel will resolve. It intercepts Qwen3's XML tool-call text and runs the full agentic loop.
        return services.AddSingleton<IChatCompletionService>(sp => new FoundryLocalChatCompletionService(sp.GetRequiredService<OpenAIChatCompletionService>()));
    }
}
