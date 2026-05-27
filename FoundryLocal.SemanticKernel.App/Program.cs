using FoundryLocal.SemanticKernel.App.ChatCompletion;
using FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;
using FoundryLocal.SemanticKernel.Implementations;
using FoundryLocal.SemanticKernel.Interfaces;
using FoundryLocal.SemanticKernel.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace FoundryLocal.SemanticKernel.App;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();

        builder.Services.AddOptions<FoundryLocalOptions>()
                .Bind(builder.Configuration.GetSection(nameof(FoundryLocalOptions)))
                .ValidateOnStart();

        builder.Services.AddSingleton<IFoundryModelService, FoundryModelService>();

        builder.Services
            .AddKernel().Plugins
                .AddFromType<DateTimePlugin>()
                .AddFromType<CalculatorPlugin>();

        var options = builder.Configuration
            .GetSection(nameof(FoundryLocalOptions))
            .Get<FoundryLocalOptions>()
                ?? throw new InvalidOperationException("Failed to bind FoundryLocalOptions from configuration.");

        // Register the inner OpenAI service by its concrete type (not as IChatCompletionService)
        // so the decorator can resolve and wrap it.
#pragma warning disable SKEXP0010
        builder.Services.AddSingleton(new OpenAIChatCompletionService(
            modelId: options.ModelAlias,
            apiKey: "not-needed",
            endpoint: new Uri($"{options.WebServiceUrl}/v1")));
#pragma warning restore SKEXP0010

        // Register the decorator as the IChatCompletionService the Kernel will resolve.
        // It intercepts Qwen3's XML tool-call text and runs the full agentic loop.
        builder.Services.AddSingleton<IChatCompletionService>(sp =>
            new QwenFunctionCallDecorator(
                sp.GetRequiredService<OpenAIChatCompletionService>()));

        builder.Services.AddHostedService<Worker>();
        var host = builder.Build();

        await host.RunAsync();
    }
}