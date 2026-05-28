# Architecture

## Component overview

```mermaid
graph TD
    App["FoundryLocal.SemanticKernel.App\n(Worker Service)"]
    Lib["FoundryLocal.SemanticKernel\n(Library)"]
    SK["Microsoft.SemanticKernel"]
    FL["Microsoft.AI.Foundry.Local"]
    Model["Qwen3.5 / other model\n(running locally)"]

    App -->|uses| Lib
    App -->|uses| SK
    Lib -->|wraps| SK
    Lib -->|manages| FL
    FL -->|inference| Model
```

The solution is split into two projects:

| Project | Role |
|---|---|
| `FoundryLocal.SemanticKernel` | Reusable library — model lifecycle, SK decorator, XML parser |
| `FoundryLocal.SemanticKernel.App` | Demo worker app — plugins, configuration, entry point |

---

## Library internals

```mermaid
classDiagram
    class IFoundryModelService {
        <<interface>>
        +GetModelAsync()
        +DownloadModelAsync()
        +LoadModelAsync()
        +UnloadModelAsync()
        +StartWebServiceWithModelAsync()
        +StopWebServiceWithModelAsync()
    }

    class FoundryModelService {
        -FoundryLocalManager _manager
        -IModel _currentModel
        +StartWebServiceWithModelAsync()
    }

    class FoundryLocalChatCompletionService {
        -IChatCompletionService _inner
        -ILogger _logger
        +GetChatMessageContentsAsync()
    }

    class ChatMessageContentExtensions {
        <<static>>
        +GetParsedToolCalls() IReadOnlyList~FunctionCallContent~
    }

    class FoundryLocalPromptExecutionSettings {
        +Temperature
        +MaxTokens
        +FunctionChoiceBehavior = Auto(autoInvoke:false)
    }

    class DependencyInjection {
        <<static>>
        +AddFoundryLocalChatCompletion()
    }

    IFoundryModelService <|.. FoundryModelService
    FoundryLocalChatCompletionService ..> ChatMessageContentExtensions : calls
    FoundryLocalChatCompletionService ..> FoundryLocalPromptExecutionSettings : normalises to
    DependencyInjection ..> FoundryLocalChatCompletionService : registers
```

---

## Request lifecycle

This is what happens from the moment a user sends a message to when they receive a final answer.

```mermaid
flowchart TD
    A([User message]) --> B[Worker.cs\nAdds message to ChatHistory]
    B --> C[FoundryLocalChatCompletionService\nNormalises settings to FoundryLocalPromptExecutionSettings]
    C --> D[OpenAIChatCompletionService\nHTTP POST /v1/chat/completions]
    D --> E{Response\ncontains\ntool call?}

    E -- No --> F([Return text to user])
    E -- Yes --> G[ChatMessageContentExtensions\nParseXML → FunctionCallContent list]
    G --> H[Add assistant tool-call request\nto ChatHistory]
    H --> I[Invoke each FunctionCallContent\nvia kernel]
    I --> J{Invocation\nsucceeded?}

    J -- Yes --> K[Add FunctionResultContent to history]
    J -- No --> L[Add structured error message to history]
    K --> M{Max iterations\nreached?}
    L --> M
    M -- No --> D
    M -- Yes --> D
    D --> F
```

**Key points:**
- The `_inner` service (`OpenAIChatCompletionService`) always runs with `autoInvoke: false` — it never tries to invoke functions itself
- `FoundryLocalChatCompletionService` owns the agentic loop and caps it at 5 iterations to prevent infinite loops
- On function invocation errors, a Markdown-formatted error message is fed back to the model so it can respond gracefully (e.g., "I was unable to read that file")

---

## Dependency injection wiring

`DependencyInjection.cs` provides one extension method:

```csharp
services.AddFoundryLocalChatCompletion(modelAlias, endpoint);
```

Internally it registers:

```
IChatCompletionService  ←  FoundryLocalChatCompletionService
                                └── OpenAIChatCompletionService (inner, not in DI)
```

The concrete `OpenAIChatCompletionService` is **not** registered in the DI container — it's created directly inside the factory lambda. This avoids conflicts if you were to also register a different `IChatCompletionService` for testing.

---

## Further reading

- [README — quick start](../README.md)
- [Function calling — the ONNX model problem and solution](function-calling.md)
- [Adding plugins](plugins.md)
