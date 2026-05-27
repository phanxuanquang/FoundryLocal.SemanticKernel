using FoundryLocal.SemanticKernel.Extensions;
using FoundryLocal.SemanticKernel.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Frozen;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace FoundryLocal.SemanticKernel.Implementations;

public sealed class FoundryLocalChatCompletionService(IChatCompletionService inner, ILogger<FoundryLocalChatCompletionService>? logger = null) : IChatCompletionService
{
    private const sbyte MaxIterations = 5;

    private readonly IChatCompletionService _inner = inner;
    private readonly ILogger<FoundryLocalChatCompletionService> _logger = logger ?? NullLogger<FoundryLocalChatCompletionService>.Instance;

    public IReadOnlyDictionary<string, object?> Attributes => _inner.Attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        executionSettings = executionSettings == null
            ? new FoundryLocalPromptExecutionSettings()
            : FoundryLocalPromptExecutionSettings.FromExecutionSettings(executionSettings);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var responses = await _inner.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);

            if (kernel is null)
                return responses;

            var toolCalls = responses
                .AsParallel()
                .SelectMany(r => r.GetParsedToolCalls())
                .ToList();

            if (toolCalls is null || toolCalls.Count == 0)
                return responses;

            _logger.LogTrace("Found {ToolCallCount} tool calls in model response, beginning iteration {Iteration}",
                toolCalls.Count, iteration + 1);
            var requestItems = new ChatMessageContentItemCollection();
            foreach (var call in toolCalls)
            {
                requestItems.Add(call);
            }

            chatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, requestItems));

            var resultItems = new ChatMessageContentItemCollection();
            foreach (var call in toolCalls)
            {
                try
                {
                    _logger.LogTrace("Invoking function **{FunctionName}** with arguments: {Arguments}",
                        call.FunctionName, call.Arguments);
                    var functionResult = await call.InvokeAsync(kernel, cancellationToken);
                    resultItems.Add(functionResult);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error invoking function **{FunctionName}** with arguments: {Arguments}",
                        call.FunctionName, call.Arguments);

                    var sb = new StringBuilder();
                    sb.AppendLine($"Error invoking the function **{call.FunctionName}**");
                    if (call.Arguments is not null && call.Arguments.Count > 0)
                    {
                        var argsDict = call.Arguments
                            .Where(kv => kv.Value is not null)
                            .ToFrozenDictionary(kv => kv.Key, kv => kv.Value?.ToString());

                        var argsJson = JsonSerializer.Serialize(argsDict, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            AllowDuplicateProperties = false
                        });

                        sb.Append(" with arguments:");
                        sb.AppendLine();
                        sb.AppendLine("```json");
                        sb.AppendLine(argsJson);
                        sb.AppendLine("```");
                    }
                    else
                    {
                        sb.Append('.');
                    }

                    sb.AppendLine();
                    sb.AppendLine("Exception message:");
                    sb.AppendLine();
                    sb.AppendLine("```");
                    sb.AppendLine(ex.Message);
                    sb.AppendLine("```");

                    var errorContent = new ChatMessageContentItemCollection
                    {
                        new TextContent(sb.ToString())
                    };

                    resultItems.Add(new ChatMessageContent(AuthorRole.Tool, errorContent));
                }
            }

            // 3. Add tool results to history and let the model continue.
            chatHistory.Add(new ChatMessageContent(AuthorRole.Tool, resultItems));
        }

        return await _inner.GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Passthrough — streaming tool-call parsing is not implemented.
        await foreach (var chunk in _inner.GetStreamingChatMessageContentsAsync(
            chatHistory, executionSettings, kernel, cancellationToken))
        {
            yield return chunk;
        }
    }
}
