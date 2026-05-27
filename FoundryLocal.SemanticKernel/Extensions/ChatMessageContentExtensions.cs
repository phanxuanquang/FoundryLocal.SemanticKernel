using Microsoft.SemanticKernel;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FoundryLocal.SemanticKernel.Extensions;

internal static partial class ChatMessageContentExtensions
{
    [GeneratedRegex(@"<tool_call>\s*<function=(?<name>[^>]+?)>(?<args>[\s\S]*?)</function>\s*</tool_call>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex ToolCallRegex();

    [GeneratedRegex(@"<parameter=(?<paramName>[^>]+?)>\s*(?<paramValue>[\s\S]*?)\s*</parameter>", RegexOptions.Compiled | RegexOptions.Singleline)]
    private static partial Regex FunctionCallParameterRegex();

    internal static IReadOnlyList<FunctionCallContent> GetParsedToolCalls(this ChatMessageContent message)
    {
        var text = message.Content;

        // Cheapest checks first: null/empty, then a fast SIMD scan for the literal "<tool_call>" before paying the cost of the full regex engine.
        if (string.IsNullOrWhiteSpace(text))
            return [];

        if (!text.Contains("<tool_call>", StringComparison.Ordinal))
            return [];

        // Only check for existing FunctionCallContent items when we know the text actually contains a tool call marker (avoids scanning Items on every response).
        if (message.Items.OfType<FunctionCallContent>().Any())
            return [];

        var toolCallMatches = ToolCallRegex().Matches(text);
        if (toolCallMatches.Count == 0)
            return [];

        var calls = new List<FunctionCallContent>(toolCallMatches.Count);

        foreach (var toolCallMatchGroups in toolCallMatches.Select(m => m.Groups))
        {
            var fullNameSpan = toolCallMatchGroups["name"].ValueSpan.Trim();
            var sep = fullNameSpan.IndexOf('-');
            var pluginName = sep > 0 ? new string(fullNameSpan[..sep]) : null;
            var functionName = sep > 0 ? new string(fullNameSpan[(sep + 1)..]) : new string(fullNameSpan);

            var argsSpan = toolCallMatchGroups["args"].ValueSpan.Trim();

            if (argsSpan.IsEmpty)
            {
                calls.Add(new FunctionCallContent(
                    functionName: functionName,
                    pluginName: pluginName,
                    id: fullNameSpan.ToString()));
                continue;
            }

            KernelArguments? arguments = null;
            if (argsSpan[0] == '<')
            {
                var toolCallParameterMatches = FunctionCallParameterRegex().Matches(toolCallMatchGroups["args"].Value);
                if (toolCallParameterMatches.Count > 0)
                {
                    arguments = [];
                    foreach (var toolCallParameterMatchGroups in toolCallParameterMatches.Select(m => m.Groups))
                    {
                        var paramName = toolCallParameterMatchGroups["paramName"].ValueSpan.Trim();
                        var paramValue = toolCallParameterMatchGroups["paramValue"].ValueSpan.Trim();
                        arguments[new string(paramName)] = new string(paramValue);
                    }
                }
            }
            else if (argsSpan[0] == '{') // Fallback: JSON format {"param": "value"}
            {
                try
                {
                    var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(toolCallMatchGroups["args"].Value);
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

            calls.Add(new FunctionCallContent(
                functionName: functionName,
                pluginName: pluginName,
                id: $"{fullNameSpan.ToString()}{(arguments is not null ? $"({string.Join(",", arguments.Select(kv => $"{kv.Key}={kv.Value}"))})" : "")}", // Example: "WeatherPlugin_GetCurrentWeather(location=Seattle)"
                arguments: arguments));
        }

        return calls;
    }
}