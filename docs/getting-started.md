# Getting started

A 10-minute path from an empty machine to a working gateway call.

## 1. Install .NET 10

AI Gateway targets **.NET 10**.

```bash
# Verify
dotnet --version
# Expected: 10.0.x
```

If you don't have it, install from <https://dotnet.microsoft.com/download/dotnet/10.0>.

## 2. Get the source

```bash
git clone https://github.com/gildofj/ai-gateway.git
cd ai-gateway
```

## 3. Provide at least one API key

The gateway discovers providers from environment variables at startup. **Without any key, the gateway will fail to start** — `TaskAnalyzer` requires at least one provider to be available.

Copy the template:

```bash
cp .env.example .env
```

Open `.env` and fill in one or more keys:

```env
OPENAI_API_KEY=sk-...
GOOGLE_API_KEY=AIza...
ANTHROPIC_API_KEY=sk-ant-...
```

> AI Gateway does not auto-load `.env` files. Use a tool of your choice to export them, for example:
>
> - **PowerShell**: `Get-Content .env | ForEach-Object { if ($_ -match '^(\w+)=(.+)$') { Set-Item "env:$($Matches[1])" $Matches[2] } }`
> - **bash**: `set -a && source .env && set +a`
> - or define the variables in `src/AiGateway.Api/appsettings.Development.json` under `AI:<Provider>:ApiKey` (do not commit them).

Where to get keys:

| Provider | Console |
|---|---|
| OpenAI | <https://platform.openai.com/api-keys> |
| Anthropic | <https://console.anthropic.com/settings/keys> |
| Google AI Studio | <https://aistudio.google.com/apikey> |

## 4. Build

```bash
dotnet build src/AiGateway.Api/AiGateway.Api.csproj
```

Expected: build succeeded with **0 warnings**. If you see warnings, please open an issue.

## 5. Run

```bash
dotnet run --project src/AiGateway.Api/AiGateway.Api.csproj
```

You should see:

```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5042
info: Microsoft.Hosting.Lifetime[0]
      Application started.
```

For hot reload during development:

```bash
dotnet watch --project src/AiGateway.Api/AiGateway.Api.csproj
```

## 6. Make your first request

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Write a haiku about Friday afternoons."}'
```

Sample response:

```json
{
  "completion": "Sunlight slips through blinds — ...",
  "modelUsed": "claude-3-haiku-20240307",
  "providerUsed": "Anthropic",
  "domain": "Writing",
  "enhancedPrompt": "Compose a haiku ...",
  "usage": { "inputTokens": 38, "outputTokens": 22, "totalTokens": 60 },
  "estimatedCost": 0.0000370
}
```

What happened behind the scenes:

1. `TaskAnalyzer` ran a free heuristic, then classified the prompt as `Writing` / `Low`.
2. `AgentSelector` picked the `WritingAgent`, which prefers Anthropic.
3. `PromptEnhancer` rewrote the prompt with a writing-specific hint.
4. `AgentOptimizationClient` prepended the writing system prompt and pruned context.
5. The request went to Claude Haiku. The response came back with usage tokens.
6. `CostTracker` converted tokens to USD.

## 7. Pin the route (optional)

If you don't want the gateway to classify the prompt, pass `domain`, `complexity`, and `provider` explicitly:

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Explain monads in 3 sentences.",
    "domain": "Coding",
    "complexity": "Low",
    "provider": "OpenAi"
  }'
```

When both `domain` and `complexity` are provided, the classifier AI call is **skipped entirely** — saving one round-trip.

## 8. Inspect the OpenAPI document

In development, the OpenAPI document is served at:

```
http://localhost:5042/openapi/v1.json
```

Import it into Postman, Bruno, or your IDE's REST client.

## Next steps

- [Configuration](configuration.md) — every env var and override
- [API reference](api-reference.md) — request and response schema
- [Routing & cost](routing-and-cost.md) — how domains map to providers and what each call costs
- [Architecture](architecture.md) — the pipeline in detail
- [Contributing](../CONTRIBUTING.md) — add a provider, agent, or skill
