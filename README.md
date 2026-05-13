<div align="center">

# AI Gateway

**One endpoint. Many models. Smart routing.**

A provider-agnostic AI gateway built on .NET 10 that classifies each prompt, picks the right model for the job, enhances the prompt, and falls back automatically when a provider goes down.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg)](CONTRIBUTING.md)
[![Code of Conduct](https://img.shields.io/badge/Contributor%20Covenant-2.1-4baaaa.svg)](CODE_OF_CONDUCT.md)

[Quickstart](#-quickstart) · [Architecture](docs/architecture.md) · [API Reference](docs/api-reference.md) · [Deploy](docs/deployment.md) · [Contributing](CONTRIBUTING.md) · [Roadmap](docs/roadmap.md)

</div>

---

## Why AI Gateway

Different prompts deserve different models. Sending a "hi" to GPT‑4o is wasteful; sending a hard refactor to a cheap model is unreliable. AI Gateway sits between your app and the major AI providers (OpenAI, Anthropic, Google) and makes that choice for you — per request, in milliseconds.

- **Domain-aware routing** — eight built-in domain agents (Coding, Research, Writing, Analysis, Math, Translation, Conversation, General) each prefer specific providers and inject specialized system prompts.
- **Two-tier complexity** — `Low` (fast / cheap) and `High` (capable / expensive) model selection happens automatically.
- **Zero-cost analysis when you know the answer** — pass `domain` and `complexity` explicitly and the classifier AI call is skipped.
- **Resilient by default** — `FallbackChatClient` transparently retries on the next available provider when the primary one fails.
- **Skills as tools** — `AIFunction`-based skills (code search, web search, memory, time) are injected only when the selected agent needs them.
- **Cost-aware** — every response includes token usage and an estimated dollar cost.

## ✨ Features

| Capability | Status |
|---|---|
| OpenAI / Anthropic / Google providers via the OpenAI-compatible SDK | ✅ |
| Automatic prompt classification (`TaskAnalyzer`) with a free heuristic fast-path | ✅ |
| Pluggable domain agents (one class implementing `IDomainAgent`) | ✅ |
| Pluggable skills (any static class returning `AITool[]`) | ✅ |
| Prompt enhancement layer (`PromptEnhancer`) | ✅ |
| Provider fallback on transport errors and timeouts | ✅ |
| Context pruning (system + last 6 messages) | ✅ |
| Per-provider optimizations (concise hints for Claude, tool guidance for Gemini) | ✅ |
| Token usage and per-request cost estimate | ✅ |
| OpenAPI / Swagger UI in development | ✅ |
| Local Ollama provider | 🚧 planned |
| Streaming responses | 🚧 planned |
| Semantic caching | 🚧 planned |

See the full [roadmap](docs/roadmap.md).

## 🚀 Quickstart

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- At least one provider API key: OpenAI, Anthropic, or Google AI Studio

### 1. Clone and configure

```bash
git clone https://github.com/gildofj/ai-gateway.git
cd ai-gateway
cp .env.example .env
# Open .env and paste at least one API key
```

### 2. Run

```bash
dotnet run --project src/AiGateway.Api/AiGateway.Api.csproj
```

The API is now live at `http://localhost:5042`. Swagger UI is available at `http://localhost:5042/openapi/v1.json` in development.

### 3. Call it

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Refactor this method to use LINQ: ..."
  }'
```

Response:

```json
{
  "completion": "Here is the refactored version ...",
  "modelUsed": "claude-3-5-sonnet-20241022",
  "providerUsed": "Anthropic",
  "domain": "Coding",
  "enhancedPrompt": "Refactor the following C# method ...",
  "usage": { "inputTokens": 412, "outputTokens": 188, "totalTokens": 600 },
  "estimatedCost": 0.004056
}
```

The gateway:
1. Classified the prompt as `Coding` / `High` complexity.
2. Picked Anthropic Claude Sonnet (the `CodingAgent`'s preferred provider).
3. Enhanced the prompt with a domain-specific hint.
4. Injected the coding system prompt + the `code` and `memory` skills.
5. Returned the completion alongside cost and usage.

### Run in Docker (optional)

```bash
docker build -t ai-gateway .
docker run --rm -p 8080:8080 \
  -e OPENAI_API_KEY=$OPENAI_API_KEY \
  ai-gateway
# → http://localhost:8080
```

The same image is what gets deployed to Cloud Run. CI builds on a GitHub-hosted runner and pushes straight to Artifact Registry — no Cloud Build, designed to stay inside the free tier. See [deployment](docs/deployment.md).

### With make

If you have GNU Make installed, the bundled `Makefile` shortens every common command:

```bash
make           # show all targets
make watch     # run with hot reload
make docker-run
make deploy-manual
make logs-tail
```

## 🧭 How it works

```
POST /api/v1/chat/completions
    │
    ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│  TaskAnalyzer    │───▶│  AgentSelector   │───▶│  PromptEnhancer  │
│  domain + level  │    │  routing decision│    │  rewrite (opt)   │
└──────────────────┘    └──────────────────┘    └──────────────────┘
                                                          │
                                                          ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ Provider call    │◀───│ AgentOptimization│◀───│  ModelRouter     │
│ (OpenAI/Claude/  │    │ system + prune   │    │ +FallbackClient  │
│  Gemini)         │    │ +Skills/Tools    │    │                  │
└──────────────────┘    └──────────────────┘    └──────────────────┘
```

Full breakdown in [`docs/architecture.md`](docs/architecture.md).

## 📚 Documentation

| Guide | What you'll learn |
|---|---|
| [Getting Started](docs/getting-started.md) | Install, configure, first request |
| [Architecture](docs/architecture.md) | Layers, pipeline, contracts, decorators |
| [Configuration](docs/configuration.md) | Environment variables, model overrides |
| [Deployment](docs/deployment.md) | Free-tier deploy to Google Cloud Run |
| [API Reference](docs/api-reference.md) | Endpoints, request/response schema |
| [Routing & Cost](docs/routing-and-cost.md) | How domains, complexity, and pricing work |
| [Extending: Providers](docs/extending/providers.md) | Add a new AI provider |
| [Extending: Domain Agents](docs/extending/domain-agents.md) | Add a new task domain |
| [Extending: Skills](docs/extending/skills.md) | Expose a new `AIFunction` tool |
| [Roadmap](docs/roadmap.md) | Planned work, open questions |
| [FAQ](docs/faq.md) | Common questions |
| [Terraform](infra/terraform/README.md) | Platform infra as code |

## 🛡️ Security

API keys are read from environment variables and never logged. See [SECURITY.md](SECURITY.md) for the responsible disclosure process and supported versions.

## 🤝 Contributing

Contributions are welcome — bug reports, feature requests, providers, agents, skills, and docs. Start with [CONTRIBUTING.md](CONTRIBUTING.md) and the [Code of Conduct](CODE_OF_CONDUCT.md).

Quick links:

- [Good first issues](https://github.com/gildofj/ai-gateway/labels/good%20first%20issue)
- [Open a discussion](https://github.com/gildofj/ai-gateway/discussions)
- [Report a security issue](SECURITY.md)

## 🧪 Stack

- [.NET 10](https://dotnet.microsoft.com/) · ASP.NET Core minimal API
- [`Microsoft.Extensions.AI`](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai) — unified `IChatClient` abstraction
- [`Microsoft.Extensions.AI.OpenAI`](https://www.nuget.org/packages/Microsoft.Extensions.AI.OpenAI) — Gemini and Claude via OpenAI-compatible endpoints
- C# 10+ records, collection expressions, global usings

## 📄 License

[MIT](LICENSE) © AI Gateway contributors

---

<div align="center">
<sub>Built with care by <a href="https://gildofj.dev">Gildo FJ</a> and <a href="https://github.com/gildofj/ai-gateway/graphs/contributors">contributors</a>.</sub>
</div>
