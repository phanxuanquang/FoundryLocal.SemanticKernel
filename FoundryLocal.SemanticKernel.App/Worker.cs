using FoundryLocal.SemanticKernel.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using IChatCompletionService = Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService;

namespace FoundryLocal.SemanticKernel.App;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;
    private readonly IFoundryModelService _modelService;

    public Worker(ILogger<Worker> logger, IFoundryModelService modelService, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _modelService = modelService;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting web service with model '{ModelAlias}'...", _modelService.ModelAlias);
        await _modelService.StartWebServiceWithModelAsync(stoppingToken);

        await using var scope = _scopeFactory.CreateAsyncScope();
        var kernel = scope.ServiceProvider.GetRequiredService<Kernel>();
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
            Temperature = 0.7,
        };

        var chatHistory = new ChatHistory("You are a helpful assistant that can provide the current date and time, as well as weather information for a given location. /no_think");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.Write("> You: ");
                var prompt = Console.ReadLine()!;

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    Console.WriteLine("Please enter a valid prompt.");
                    continue;
                }

                chatHistory.AddUserMessage(prompt);

                var response = await chatCompletionService.GetChatMessageContentAsync(chatHistory, settings, kernel, stoppingToken);

                chatHistory.Add(response);
                Console.WriteLine($"> Assistant: {response}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat completion.");
            }
        }
    }
}