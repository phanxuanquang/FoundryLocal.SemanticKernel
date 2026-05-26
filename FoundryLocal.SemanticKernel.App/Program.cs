using FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;
using FoundryLocal.SemanticKernel.Implementations;
using FoundryLocal.SemanticKernel.Interfaces;
using FoundryLocal.SemanticKernel.Options;
using Microsoft.SemanticKernel;

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
                .AddFromType<DateTimePlugin>();

        var options = builder.Configuration
            .GetSection(nameof(FoundryLocalOptions))
            .Get<FoundryLocalOptions>()
                ?? throw new InvalidOperationException("Failed to bind FoundryLocalOptions from configuration.");
        builder.Services.AddOpenAIChatCompletion(options.ModelAlias, new Uri($"{options.WebServiceUrl}/v1"));

        builder.Services.AddHostedService<Worker>();
        var host = builder.Build();

        await host.RunAsync();
    }
}