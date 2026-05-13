# Configuration

AI Gateway is configured through environment variables and `appsettings.json`. **Environment variables take precedence.** Providers are discovered at startup — providers without a valid key are silently skipped.

## Quick reference

| Variable | Maps to | Required | Default |
|---|---|---|---|
| `OPENAI_API_KEY` | `AI:OpenAi:ApiKey` | one provider key is required | — |
| `ANTHROPIC_API_KEY` | `AI:Anthropic:ApiKey` | — | — |
| `GOOGLE_API_KEY` | `AI:Google:ApiKey` | — | — |
| `GATEWAY_API_KEY` | `GATEWAY_API_KEY` | **strongly recommended in prod** | — |
| `AI__OpenAi__FastModel` | `AI:OpenAi:FastModel` | no | `gpt-4o-mini` |
| `AI__OpenAi__CapableModel` | `AI:OpenAi:CapableModel` | no | `gpt-4o` |
| `AI__Google__FastModel` | `AI:Google:FastModel` | no | `gemini-2.0-flash` |
| `AI__Google__CapableModel` | `AI:Google:CapableModel` | no | `gemini-2.0-pro` |
| `AI__Anthropic__FastModel` | `AI:Anthropic:FastModel` | no | `claude-3-haiku-20240307` |
| `AI__Anthropic__CapableModel` | `AI:Anthropic:CapableModel` | no | `claude-3-5-sonnet-20241022` |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | no | `Production` |
| `ASPNETCORE_URLS` | listening URLs | no | `http://localhost:5042` |
| `PORT` | container port (overrides `ASPNETCORE_URLS`) | no | unset (set by Cloud Run / container hosts) |

> ASP.NET reads nested keys from environment variables using `__` as a separator. `AI__OpenAi__FastModel` becomes `AI:OpenAi:FastModel` in config.

## Where keys come from

`ProviderRegistry` reads each provider in this order:

1. Configuration path (`appsettings.json` → `AI:<Provider>:ApiKey`).
2. Environment variable.

If a key is missing, empty, or contains the substring `placeholder`, the provider is skipped. This makes it safe to keep blank entries in `appsettings.json`.

## Model selection

Each provider has two model slots:

| Slot | When used | Picked by |
|---|---|---|
| `FastModel` | `ModelComplexity.Low` — small / casual / translation prompts | `TaskAnalyzer` or explicit `complexity: "Low"` |
| `CapableModel` | `ModelComplexity.High` — coding, math, analysis, long writing | `TaskAnalyzer` or explicit `complexity: "High"` |

Override per environment by setting the relevant env var. Example: pin Anthropic to a specific Sonnet revision:

```bash
export AI__Anthropic__CapableModel=claude-3-7-sonnet-20250219
```

## OpenAI-compatible endpoints

The OpenAI SDK is used as the common transport. Endpoints are wired in `ProviderRegistry.DiscoverProviders`:

| Provider | Endpoint |
|---|---|
| OpenAI | (SDK default) |
| Google | `https://generativelanguage.googleapis.com/v1beta/openai/` |
| Anthropic | `https://api.anthropic.com/v1/messages/openai/` |

If a provider publishes a new compatibility URL, change it in `ProviderRegistry.cs` only — no other code needs to know.

## Provider preferences per domain

Each domain agent declares a `PreferredProviders` list. The first one whose key is configured wins. If none are configured, the gateway falls back to whichever provider it discovered first.

| Agent | Preferred order |
|---|---|
| `CodingAgent` | Anthropic → OpenAI |
| `ResearchAgent` | Google → OpenAI |
| `WritingAgent` | Anthropic → OpenAI |
| `AnalysisAgent` | Anthropic → Google |
| `MathAgent` | OpenAI → Google |
| `TranslationAgent` | Google → OpenAI |
| `ConversationAgent` | Google → OpenAI → Anthropic |
| `GeneralAgent` | OpenAI → Google |

Override per request with `"provider": "OpenAi"` in the request body.

## Logging

AI Gateway uses the standard ASP.NET logging. Adjust verbosity in `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "AiGateway.Api.Infrastructure.AiProviders.FallbackChatClient": "Debug"
    }
  }
}
```

`FallbackChatClient` emits a `Warning` whenever it swaps providers — useful when investigating flaky upstreams.

## URLs and TLS

Set `ASPNETCORE_URLS` to control where the gateway listens:

```bash
export ASPNETCORE_URLS="http://0.0.0.0:5042;https://0.0.0.0:7036"
```

In development, `launchSettings.json` provides `http` and `https` profiles. In production, terminate TLS at a reverse proxy (nginx, Caddy, Cloud Run, App Service, ...).

When running in a container, the host platform usually injects `PORT` and expects the app to bind to it. The gateway honors this automatically — `Program.cs` reads `PORT` and overrides `ASPNETCORE_URLS` when set. The bundled `Dockerfile` exposes `8080` (Cloud Run's default). See [deployment](deployment.md) for the end-to-end path.

## Gateway authentication

When `GATEWAY_API_KEY` is set, every `/api/*` call must carry a matching `X-API-Key` header (constant-time compare). Without the header — or with a wrong value — the gateway returns `401 Unauthorized` before reaching any upstream LLM. This is what keeps the public Cloud Run URL from draining the free tier.

```bash
curl -X POST https://your-gateway/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $GATEWAY_API_KEY" \
  -d '{"prompt":"Hi"}'
```

When the variable is **not** set:
- In `Development`, the gate is silently skipped (convenient for local testing).
- In any other environment, the gateway logs a `Warning` on startup pointing out the exposure.

Generate a key with `openssl rand -hex 32` and store it in Secret Manager (production) or `.env` (local).

## Security

- Never commit a real key. `.env` is ignored by `.gitignore`.
- Treat `appsettings.Development.json` as machine-local — do not commit keys there either.
- Rotate keys on every personnel change. See [`../SECURITY.md`](../SECURITY.md) for the full policy.
