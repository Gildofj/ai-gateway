# Extending: add a new provider

This guide walks through adding a brand-new AI provider — for example, Mistral or Ollama — to AI Gateway.

> Companion checklist for AI assistants: [`.agents/skills/add-provider.md`](../../.agents/skills/add-provider.md).

## When does a provider belong in core?

A provider belongs **in core** when:

- Its API surface is OpenAI-compatible (a custom `Endpoint` is enough), **or**
- It's significant enough that ≥ 1 domain agent would prefer it.

Otherwise, a community-maintained adapter project is a better home. Open a [discussion](../../../discussions) before starting if unsure.

## 1. Reserve the enum value

`src/AiGateway.Api/Core/Models/AiProvider.cs`

```csharp
public enum AiProvider
{
    OpenAi,
    Anthropic,
    Google,
    Ollama,
    Mistral,   // ← new
}
```

> Add new values at the end. The values are persisted in responses, so reordering would be a breaking change.

## 2. Register discovery

`src/AiGateway.Api/Infrastructure/Configuration/ProviderRegistry.cs` — inside `DiscoverProviders`:

```csharp
TryAdd(providers, config, AiProvider.Mistral,
    keyPath: "AI:Mistral:ApiKey",
    envVar: "MISTRAL_API_KEY",
    defaultFast: "mistral-small-latest",
    defaultCapable: "mistral-large-latest",
    endpoint: new Uri("https://api.mistral.ai/v1/"));
```

Rules:

- Pick a `defaultFast` and `defaultCapable` that map to a real cheap and a real capable model.
- The `endpoint` must be OpenAI-compatible. If the provider doesn't speak the OpenAI dialect, you'll need a real adapter — open a discussion first.
- The `envVar` follows the existing pattern: `<PROVIDER>_API_KEY`.

## 3. Surface defaults in `appsettings.json`

`src/AiGateway.Api/appsettings.json`

```jsonc
"AI": {
  // ... existing providers ...
  "Mistral": {
    "ApiKey": "",
    "FastModel": "mistral-small-latest",
    "CapableModel": "mistral-large-latest"
  }
}
```

Keep `ApiKey` empty so the provider is **skipped by default** until the operator provides a real key.

## 4. Update `.env.example`

`.env.example`

```env
MISTRAL_API_KEY=
# AI__Mistral__FastModel=mistral-small-latest
# AI__Mistral__CapableModel=mistral-large-latest
```

## 5. Add pricing to `CostTracker`

`src/AiGateway.Api/Infrastructure/Cost/CostTracker.cs`

```csharp
var pricing = model.ToLowerInvariant() switch
{
    // ... existing entries ...
    var m when m.Contains("mistral-small") => (input: 0.20m, output: 0.60m),
    var m when m.Contains("mistral-large") => (input: 2.00m, output: 6.00m),
    _ => (input: 1.00m, output: 3.00m)
};
```

> Pricing is in **USD per 1 million tokens**. Always pull values from the provider's official pricing page on the day you submit the PR — and include the source link in the PR description.

## 6. (Optional) Wire per-provider optimizations

If the provider needs special handling (concise hints, tool-use guidance, JSON-mode quirks), add a case to `ProviderOptimizationClient.ApplyOptimizations`:

```csharp
case "mistral":
    // Mistral does well with explicit step-by-step instructions for code tasks.
    break;
```

The `_providerName` is derived from `AiProvider.ToString().ToLower()` in `ProviderRegistry.CreateClient` — no extra wiring needed.

## 7. (Optional) Update agent preferences

If your new provider is the best in class on a domain, add it to the relevant agent's `PreferredProviders`:

```csharp
// Example: Mistral is a strong open-weights coder
public IReadOnlyList<AiProvider> PreferredProviders =>
    [AiProvider.Anthropic, AiProvider.Mistral, AiProvider.OpenAi];
```

Don't reorder lightly — preferred-provider order is a routing decision. If you have benchmark data, link it in the PR.

## 8. Smoke test

```bash
export MISTRAL_API_KEY=...
dotnet run --project src/AiGateway.Api/AiGateway.Api.csproj

curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Write a one-line bash command to print today's date.",
    "provider": "Mistral",
    "complexity": "Low"
  }'
```

Verify:

- `providerUsed` is `"Mistral"`.
- `modelUsed` is `mistral-small-latest`.
- `estimatedCost` is non-null and roughly matches the pricing table.

## 9. Document

- Add an entry in [`../configuration.md`](../configuration.md) under "Quick reference".
- Add an entry in [`../routing-and-cost.md`](../routing-and-cost.md) under "Complexity → model".
- Add an entry in `CHANGELOG.md` under `## [Unreleased]` → `### Added`.

## 10. Open the PR

Use the **Provider request** issue template if you're proposing first. When the PR is ready, follow [`CONTRIBUTING.md`](../../CONTRIBUTING.md#pull-requests).

## Checklist

- [ ] Enum value appended (not reordered).
- [ ] `ProviderRegistry.TryAdd` call.
- [ ] `appsettings.json` defaults with empty key.
- [ ] `.env.example` updated.
- [ ] `CostTracker` entry with source link in PR.
- [ ] Optional: per-provider optimization branch.
- [ ] Optional: agent preferences updated, with benchmark justification.
- [ ] Manual smoke test against a real key.
- [ ] Docs + changelog updated.
- [ ] Build is **0 warnings**.
