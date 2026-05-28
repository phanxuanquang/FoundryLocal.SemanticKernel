# Plugins

Plugins are the way you give the AI model access to real functionality — reading a file, calling an API, doing a calculation, etc. Semantic Kernel discovers plugins at startup and advertises them to the model as "tools" it can call.

---

## Built-in plugins (demo app)

| Plugin class | SK function name | What it does |
|---|---|---|
| `DateTimePlugin` | `GetCurrentDateTime` | Returns current local date/time as `yyyy-MM-dd HH:mm:ss` |
| `CalculatorPlugin` | `calculate` | Evaluates a math expression string (e.g. `"12 * 7 + 3"`) |
| `FilePlugin` | `read_text_file` | Reads all text from a file at the given path |

---

## How to add a plugin

### 1. Create the class

```csharp
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FoundryLocal.SemanticKernel.App.SemanticKernelPlugins;

[Description("Gets current weather information.")]
public sealed class WeatherPlugin(ILogger<WeatherPlugin> logger)
{
    [KernelFunction("get_weather")]
    [Description("Returns the current weather for a given city.")]
    public string GetWeather(
        [Description("The city name, e.g. 'London'")] string city)
    {
        logger.LogInformation("Getting weather for {City}", city);
        // Your implementation here
        return $"Sunny, 22°C in {city}";
    }
}
```

**Key attributes:**

| Attribute | Required | Purpose |
|---|---|---|
| `[Description]` on the class | Recommended | Tells the model what the plugin is for |
| `[KernelFunction]` on the method | **Required** | Marks the method as callable by the model |
| `[Description]` on the method | **Required** | Tells the model when to use this function |
| `[Description]` on each parameter | Recommended | Tells the model what value to pass |

> The descriptions are included in the prompt sent to the model. Clear, specific descriptions lead to more reliable function invocations.

### 2. Register it in `Program.cs`

```csharp
builder.Services
    .AddKernel().Plugins
        .AddFromType<DateTimePlugin>()
        .AddFromType<CalculatorPlugin>()
        .AddFromType<FilePlugin>()
        .AddFromType<WeatherPlugin>();   // ← add here
```

That's it. On the next run, the model will be able to call `WeatherPlugin-get_weather`.

---

## How the model calls your function

The model uses your class name and function name separated by `-` as the tool identifier. For example:

| Class | Method (KernelFunction name) | Tool identifier |
|---|---|---|
| `WeatherPlugin` | `get_weather` | `WeatherPlugin-get_weather` |
| `DateTimePlugin` | `GetCurrentDateTime` | `DateTimePlugin-GetCurrentDateTime` |

The model output looks like:

```
<tool_call>
<function=WeatherPlugin-get_weather>
<parameter=city>
London
</parameter>
</function>
</tool_call>
```

`ChatMessageContentExtensions.GetParsedToolCalls()` parses this and Semantic Kernel invokes `WeatherPlugin.GetWeather("London")` automatically.

---

## Async plugins

For plugins that do I/O (HTTP calls, file reads, DB queries), use `async Task<string>`:

```csharp
[KernelFunction("fetch_url")]
[Description("Fetches the text content of a URL.")]
public async Task<string> FetchUrlAsync(
    [Description("The URL to fetch")] string url,
    CancellationToken cancellationToken = default)
{
    using var http = new HttpClient();
    return await http.GetStringAsync(url, cancellationToken);
}
```

> Semantic Kernel automatically handles `Task<string>` return types — you don't need to do anything special.

---

## Return types

SK can handle these return types from plugin methods:

| Return type | Behaviour |
|---|---|
| `string` | Passed directly to the model |
| `Task<string>` | Awaited, then passed to the model |
| Any other type | Converted via `.ToString()` |

For structured results, serialize to JSON yourself and return the JSON string — this gives the model the richest information to work with.

---

## Security note for `FilePlugin`

The built-in `FilePlugin` has no path restrictions. In a demo this is fine, but if you expose this to untrusted input, consider:

- Whitelisting allowed directories
- Rejecting paths that contain `..` (path traversal)
- Limiting file size before reading

```csharp
// Example: restrict to a safe folder
private static readonly string AllowedRoot = Path.GetFullPath("C:\\MyApp\\Data");

public async Task<string> ReadTextFileAsync(string path)
{
    var fullPath = Path.GetFullPath(path);
    if (!fullPath.StartsWith(AllowedRoot, StringComparison.OrdinalIgnoreCase))
        return "Access denied.";

    return await File.ReadAllTextAsync(fullPath);
}
```

---

## Further reading

- [Semantic Kernel plugins overview](https://learn.microsoft.com/en-us/semantic-kernel/concepts/plugins/)
- [Function calling in SK](https://learn.microsoft.com/en-us/semantic-kernel/concepts/ai-services/chat-completion/function-calling)
- [How function calls are parsed in this library](function-calling.md)
