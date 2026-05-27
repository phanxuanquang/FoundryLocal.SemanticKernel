using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FoundryLocal.SemanticKernel.App.ChatCompletion;

/// <summary>
/// Wraps an <see cref="IChatCompletionService"/> to handle Qwen3's non-standard
/// XML tool-call format. Qwen3.5-4b emits tool calls as plain text content:
/// <code>
/// &lt;tool_call&gt;
///   &lt;function=PluginName-FunctionName&gt;
///     &lt;parameter=paramName&gt;value&lt;/parameter&gt;
///   &lt;/function&gt;
/// &lt;/tool_call&gt;
/// </code>
/// instead of the standard OpenAI <c>tool_calls</c> JSON array.
/// This decorator intercepts those responses, parses the XML into
/// <see cref="FunctionCallContent"/> items, invokes the kernel functions, and
/// continues the agentic loop until a final text response is returned.
/// </summary>
internal sealed class QwenFunctionCallDecorator : IChatCompletionService
{
    private const int MaxIterations = 5;

    private static readonly Regex ToolCallRegex = new(
        @"<tool_call>\s*<function=(?<name>[^>]+?)>(?<args>[\s\S]*?)</function>\s*</tool_call>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    // Matches <parameter=name>value</parameter> inside a function block.
    private static readonly Regex ParameterRegex = new(
        @"<parameter=(?<paramName>[^>]+?)>\s*(?<paramValue>[\s\S]*?)\s*</parameter>",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private readonly IChatCompletionService _inner;

    public QwenFunctionCallDecorator(IChatCompletionService inner) => _inner = inner;

    public IReadOnlyDictionary<string, object?> Attributes => _inner.Attributes;

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // Forward tool definitions to the model but disable inner auto-invoke —
        // we manage the full agentic loop ourselves.
        var innerSettings = BuildInnerSettings(executionSettings);

        for (var iteration = 0; iteration < MaxIterations; iteration++)
        {
            var results = await _inner.GetChatMessageContentsAsync(
                chatHistory, innerSettings, kernel, cancellationToken);

            var response = results[0];

            // Try to extract Qwen-style XML tool calls from the text content.
            var toolCalls = TryParseXmlToolCalls(response);

            // No tool calls present, or kernel unavailable for invocation → final answer.
            if (toolCalls is null || kernel is null)
                return results;

            // 1. Add the assistant's tool-call request to history.
            var requestItems = new ChatMessageContentItemCollection();
            foreach (var call in toolCalls)
                requestItems.Add(call);

            chatHistory.Add(new ChatMessageContent(AuthorRole.Assistant, requestItems));

            // 2. Invoke each requested function and collect the results.
            var resultItems = new ChatMessageContentItemCollection();
            foreach (var call in toolCalls)
            {
                var functionResult = await call.InvokeAsync(kernel, cancellationToken);
                resultItems.Add(functionResult);
            }

            // 3. Add tool results to history and let the model continue.
            chatHistory.Add(new ChatMessageContent(AuthorRole.Tool, resultItems));
        }

        // Max iterations reached — return one last model response.
        return await _inner.GetChatMessageContentsAsync(
            chatHistory, innerSettings, kernel, cancellationToken);
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

    /// <summary>
    /// Copies the caller's settings but forces <c>autoInvoke: false</c> so the
    /// underlying <see cref="OpenAIChatCompletionService"/> does not run its own
    /// (broken) agentic loop while still advertising tool definitions to the model.
    /// </summary>
    private static OpenAIPromptExecutionSettings BuildInnerSettings(
        PromptExecutionSettings? source)
    {
        var inner = source is OpenAIPromptExecutionSettings oai
            ? new OpenAIPromptExecutionSettings
            {
                Temperature = oai.Temperature,
                TopP = oai.TopP,
                MaxTokens = oai.MaxTokens,
                StopSequences = oai.StopSequences,
                FrequencyPenalty = oai.FrequencyPenalty,
                PresencePenalty = oai.PresencePenalty,
            }
            : new OpenAIPromptExecutionSettings();

        // Auto with autoInvoke=false → tools ARE advertised to the model,
        // but SK will not attempt to invoke them (we handle that above).
        inner.FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(autoInvoke: false);
        return inner;
    }

    /// <summary>
    /// Parses Qwen3-style XML tool calls out of <paramref name="message"/>'s text content.
    /// Returns <c>null</c> when no XML tool calls are present.
    /// </summary>
    private static List<FunctionCallContent>? TryParseXmlToolCalls(ChatMessageContent message)
    {
        var text = message.Content;

        // Cheapest checks first: null/empty, then a fast SIMD scan for the literal
        // "<tool_call>" before paying the cost of the full regex engine.
        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (!text.Contains("<tool_call>", StringComparison.Ordinal))
            return null;

        // Only check for existing FunctionCallContent items when we know the text
        // actually contains a tool call marker (avoids scanning Items on every response).
        if (message.Items.OfType<FunctionCallContent>().Any())
            return null;

        var matches = ToolCallRegex.Matches(text);
        if (matches.Count == 0)
            return null;

        var calls = new List<FunctionCallContent>(matches.Count);

        foreach (Match match in matches)
        {
            var groups = match.Groups;

            // Use ValueSpan to avoid allocating a trimmed string for the name.
            var fullNameSpan = groups["name"].ValueSpan.Trim();
            var sep = fullNameSpan.IndexOf('-');
            var pluginName = sep > 0 ? new string(fullNameSpan[..sep]) : null;
            var functionName = sep > 0 ? new string(fullNameSpan[(sep + 1)..]) : new string(fullNameSpan);

            KernelArguments? arguments = null;
            var argsSpan = groups["args"].ValueSpan.Trim();
            if (!argsSpan.IsEmpty)
            {
                // Route by first character — avoids running the wrong parser entirely.
                if (argsSpan[0] == '<')
                {
                    // Primary format: <parameter=name>value</parameter>
                    var paramMatches = ParameterRegex.Matches(groups["args"].Value);
                    if (paramMatches.Count > 0)
                    {
                        arguments = [];
                        foreach (Match pm in paramMatches)
                        {
                            var paramName = pm.Groups["paramName"].ValueSpan.Trim();
                            var paramValue = pm.Groups["paramValue"].ValueSpan.Trim();
                            arguments[new string(paramName)] = new string(paramValue);
                        }
                    }
                }
                else if (argsSpan[0] == '{')
                {
                    // Fallback: JSON format {"param": "value"}
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(groups["args"].Value);
                        if (dict is { Count: > 0 })
                        {
                            arguments = [];
                            foreach (var (key, value) in dict)
                            {
                                arguments[key] = value.ValueKind == JsonValueKind.String
                                    ? value.GetString()
                                    : value.ToString();
                            }
                        }
                    }
                    catch (JsonException) { /* ignore malformed args — invoke with no arguments */ }
                }
            }

            calls.Add(new FunctionCallContent(
                functionName: functionName,
                pluginName: pluginName,
                id: Guid.NewGuid().ToString("N"),
                arguments: arguments));
        }

        return calls.Count > 0 ? calls : null;
    }
}
