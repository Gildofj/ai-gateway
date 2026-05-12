# Skill: add-provider

Procedimento para adicionar um novo provider de AI ao gateway. Exatamente 4 arquivos.

---

## Passo 1 — `Core/Models/AiProvider.cs`

Adicionar o novo valor ao enum:

```csharp
public enum AiProvider
{
    OpenAi,
    Anthropic,
    Google,
    Ollama,
    MyProvider   // <- adicionar aqui
}
```

---

## Passo 2 — `Infrastructure/Configuration/ProviderRegistry.cs`

Adicionar uma chamada `TryAdd` no método `DiscoverProviders`:

```csharp
TryAdd(providers, config, AiProvider.MyProvider,
    keyPath: "AI:MyProvider:ApiKey",
    envVar: "MYPROVIDER_API_KEY",
    defaultFast: "my-fast-model",
    defaultCapable: "my-capable-model",
    endpoint: new Uri("https://api.myprovider.com/v1/"));  // se OpenAI-compatível
```

**Providers OpenAI-compatíveis** (usam `OpenAI.Chat.ChatClient` com endpoint customizado):
- Ollama: `http://localhost:11434/v1/`
- Mistral: `https://api.mistral.ai/v1/`
- Groq: `https://api.groq.com/openai/v1/`

Se o provider NÃO for compatível com a API OpenAI, é necessário um `IChatClient` customizado — consulte o `gateway-architect` agent.

---

## Passo 3 — `appsettings.json`

Adicionar a seção de configuração:

```json
"MyProvider": {
  "ApiKey": "",
  "FastModel": "my-fast-model",
  "CapableModel": "my-capable-model"
}
```

---

## Passo 4 — `.env.example`

```
MYPROVIDER_API_KEY=
```

---

## Verificação

```bash
dotnet build src/AiGateway.Api/AiGateway.Api.csproj
```

Confirmar que o provider aparece nos logs de startup quando a key está configurada.

---

## Atualizar domain agents (opcional)

Se o novo provider é melhor que os existentes para algum domínio, atualizar `PreferredProviders` nos agentes relevantes em `Features/Agents/`.
