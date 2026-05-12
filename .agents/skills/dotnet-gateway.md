# Skill: dotnet-gateway

Padrões e convenções específicas do AI Gateway. Leia antes de escrever qualquer código neste projeto.

---

## Padrão: IDomainAgent

```csharp
public class MyAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.MyDomain;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.OpenAi, AiProvider.Google];
    public string SystemPromptFragment => "Role. Behavior. Constraint.";
    public IReadOnlyList<string> RequiredSkills => ["code"];     // or []
    public string EnhancementHint => "What context is usually missing for this domain.";
}
```

- **Sem construtor**, sem dependências injetadas, sem lógica
- Registrar como `AddSingleton<IDomainAgent, MyAgent>()` em `Program.cs`

---

## Padrão: DelegatingChatClient

```csharp
public class MyMiddleware : DelegatingChatClient
{
    private readonly string _config;

    public MyMiddleware(IChatClient inner, string config) : base(inner)
    {
        _config = config;
    }

    public override async Task<Microsoft.Extensions.AI.ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        // mutate list here
        return await base.GetResponseAsync(list, options, cancellationToken);
    }
}
```

- Sempre use `Microsoft.Extensions.AI.ChatResponse` fully qualified para evitar conflito com `AiGateway.Api.Core.Models.ChatResponse`
- `base.GetResponseAsync(list, ...)` — passe a lista mutada, não `messages`

---

## Padrão: AIFunction (stateless)

```csharp
public static class MySkill
{
    [Description("What it does and WHEN to call it.")]
    public static string DoSomething(
        [Description("Format and meaning of param")] string input)
    {
        try { /* ... */ return result; }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    public static IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(DoSomething)
    ];
}
```

---

## Padrão: AIFunction (stateful / scoped)

```csharp
public class MySkill                    // não static
{
    private readonly Dictionary<string, string> _state = new();

    [Description("...")]
    public string StoreItem(string key, string value) { ... }

    public IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(StoreItem)   // bound to this instance
    ];
}
```

- Registrar como `builder.Services.AddScoped<MySkill>()`
- Injetar como parâmetro do endpoint lambda em `Program.cs`

---

## Padrão: ProviderRegistry extension

Para adicionar lógica de criação de client específica de um provider (ex: autenticação customizada):

```csharp
// Dentro de ProviderRegistry.CreateClient()
AiProvider.MyProvider => options is not null
    ? new OpenAI.Chat.ChatClient(modelId, credential, options).AsIChatClient()
    : new OpenAI.Chat.ChatClient(modelId, credential).AsIChatClient(),
```

---

## Alias obrigatório em Program.cs

```csharp
using GatewayResponse = AiGateway.Api.Core.Models.ChatResponse;
```

Necessário porque `Microsoft.Extensions.AI` também exporta `ChatResponse`.

---

## Regras de namespace

| Camada | Namespace |
|---|---|
| Core/Models | `AiGateway.Api.Core.Models` |
| Core/Interfaces | `AiGateway.Api.Core.Interfaces` |
| Features/Agents | `AiGateway.Api.Features.Agents` |
| Features/Routing | `AiGateway.Api.Features.Routing` |
| Infrastructure/AiProviders | `AiGateway.Api.Infrastructure.AiProviders` |
| Infrastructure/Configuration | `AiGateway.Api.Infrastructure.Configuration` |
| Infrastructure/Cost | `AiGateway.Api.Infrastructure.Cost` |
| Skills | `AiGateway.Api.Skills` |
