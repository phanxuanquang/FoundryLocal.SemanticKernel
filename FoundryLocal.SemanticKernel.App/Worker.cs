using FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;
using FoundryLocal.SemanticKernel.Interfaces;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using IChatCompletionService = Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService;

namespace FoundryLocal.SemanticKernel.App;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IFoundryModelService _modelService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly Kernel _kernel;

    public Worker(ILogger<Worker> logger, IFoundryModelService modelService, IKernelBuilder kernelBuilder)
    {
        _logger = logger;
        _modelService = modelService;

        kernelBuilder.Plugins.AddFromType<DateTimePlugin>("Time");
        kernelBuilder.Plugins.AddFromType<WeatherPlugin>("Weather");
        _kernel = kernelBuilder.Build();
        _chatCompletionService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting web service with model '{ModelAlias}'...", _modelService.ModelAlias);
        await _modelService.StartWebServiceWithModelAsync(stoppingToken);

        var settings = new OpenAIPromptExecutionSettings
        {
            FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: true),
        };

        var chatHistory = new ChatHistory("You are a helpful assistant that can provide the current date and time, as well as weather information for a given location.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.Write("> You: ");
                var prompt = Console.ReadLine()!;
                chatHistory.AddUserMessage(prompt);

                var response = await _chatCompletionService.GetChatMessageContentsAsync(chatHistory, settings, _kernel, stoppingToken);
                chatHistory.AddRange(response);
                Console.WriteLine($"> Assistant: {response.LastOrDefault()}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during chat completion.");
            }
        }
    }
}