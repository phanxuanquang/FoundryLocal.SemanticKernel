using FoundryLocal.SemanticKernel.Implementations;
using FoundryLocal.SemanticKernel.Interfaces;
using FoundryLocal.SemanticKernel.Options;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace FoundryLocal.SemanticKernel.App;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder.Services.AddOptions<FoundryLocalOptions>()
                .Bind(builder.Configuration.GetSection(nameof(FoundryLocalOptions)))
                .ValidateOnStart();

        builder.Services.AddSingleton<IFoundryModelService>(sp =>
            new FoundryModelService(
                sp.GetRequiredService<IOptions<FoundryLocalOptions>>(),
                sp.GetRequiredService<ILoggerFactory>()));
        builder.Services.AddSingleton(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();

            var options = configuration
               .GetSection(nameof(FoundryLocalOptions))
               .Get<FoundryLocalOptions>()
                   ?? throw new InvalidOperationException($"Configuration section '{nameof(FoundryLocalOptions)}' is missing or invalid.");

            return Kernel.CreateBuilder()
                .AddOpenAIChatCompletion(
                    modelId: options.ModelAliasOrId,
                    apiKey: "not-needed",
                    endpoint: new Uri(options.WebServiceUrl + "/v1"));
        });

        builder.Services.AddHostedService<Worker>();
        var host = builder.Build();

        await host.RunAsync();
    }
}