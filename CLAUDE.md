# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Agents & Skills

Before starting any non-trivial task, adopt the appropriate agent from `.agents/agents/` and read the relevant skill from `.agents/skills/`.

| Task | Agent | Skill |
|---|---|---|
| Pipeline, routing, infrastructure changes | `gateway-architect` | `dotnet-gateway` |
| Adding a new AI provider | `provider-integrator` | `add-provider` |
| Adding a new domain agent | `domain-agent-creator` | `add-domain-agent` |
| Adding a new AIFunction skill | `skill-creator` | `add-skill` |
| Reducing token usage, tuning heuristics, context pruning | `token-economy-tuner` | `tune-token-economy` |
| Debugging routing, provider selection, fallback, skills | `pipeline-debugger` | `debug-pipeline` |

## Code Style — Non-negotiable

**Comments only when strictly necessary. Default is zero.**

A comment may exist only when *why* the code is the way it is would not be obvious to a future reader who has the diff and the git log — a hidden constraint, a subtle invariant, a workaround for a specific bug, or behavior that would surprise someone reading the code cold.

Do **not** write:
- Section banners (`// ---- setup ----`, `// === handlers ===`)
- Restatements of what the code does (`// validate input`, `// loop through items`, `// returns the user`)
- Task or PR context (`// added for the X flow`, `// fixes #123`, `// see ticket Y`) — that belongs in the commit message
- Block comments above straightforward methods, classes, or properties
- Inline notes about removed code (`// previously did X`)
- Apologetic or filler comments (`// TODO: refactor later`, `// hacky but works`)

Self-explanatory identifiers carry the meaning. If you feel a comment is needed, first try renaming a variable, extracting a method, or restructuring — comments are the last resort, not the first.

**Exceptions (these are not "comments" in the sense of this rule):**
- `///` XML doc comments on **public** APIs consumed outside the assembly
- Makefile `##` help text (renders in `make help`)
- File-level header that explains a non-obvious file purpose (e.g. middleware ordering constraints)
- License headers, if the repo requires them

Before writing any comment, ask: "Would removing this confuse a careful reader?" If no, delete it.

## Commands

```bash
# Build
dotnet build src/AiGateway.Api/AiGateway.Api.csproj

# Run (development — set at least one API key first)
dotnet run --project src/AiGateway.Api/AiGateway.Api.csproj

# Run with hot reload
dotnet watch --project src/AiGateway.Api/AiGateway.Api.csproj

# Build container image (production parity)
docker build -t ai-gateway .

# Deploy to Cloud Run — CI lives in .github/workflows/deploy.yml (builds on a
# GitHub runner, pushes to Artifact Registry, calls `gcloud run deploy`). No
# Cloud Build, no paid builders.

# Manual fallback for the same flow:
make help              # list all dev/deploy targets
make watch             # dotnet watch
make docker-run        # build + run container locally
make deploy            # build → push → deploy (uses current git SHA as tag)
make deploy-manual     # same as deploy, pinned to :manual tag

# Infrastructure as code (see infra/terraform/README.md)
make tf-init
make tf-plan
make tf-apply
```

Platform infra (APIs, Artifact Registry, deployer SA, WIF, secret containers) lives in `infra/terraform/`. The Cloud Run service itself is managed by the deploy pipeline, not Terraform.

No test projects exist yet. The solution file is `AiGateway.slnx`.

## Architecture

**AI Gateway** is a .NET 10 ASP.NET Core minimal API that dynamically discovers configured AI providers from environment variables, classifies the incoming prompt into a task domain and complexity level, selects the best provider via domain-aware agents, enhances the prompt, and executes with resilience.

### Request Pipeline (`Program.cs`)

```
POST /api/v1/chat/completions
  → AppContextMiddleware — Extract X-App-Id
  → ISessionStore        — Load session if sessionId provided
  → ITaskAnalyzer        — Reuse session decision or ONE AI call
  → AgentSelector        — Picks domain agent (Custom Agent ID → app → global → built-in)
  → IPromptEnhancer      — rewrites prompt with domain hint (optional)
  → IModelRouter         — creates IChatClient wrapped in FallbackChatClient
  → AgentOptimizationClient — injects system prompt fragment, prunes context
  → provider call        (OpenAI / Gemini / Anthropic)
  → ISessionStore        — Save turn to history
```

### Layer Boundaries

| Layer | Path | Role |
|---|---|---|
| Core | `Core/Interfaces/`, `Core/Models/` | Contracts and domain types — no implementation |
| Features | `Features/Agents/`, `Features/Routing/`, `Features/PromptEnhancement/` | Application logic |
| Infrastructure | `Infrastructure/AiProviders/`, `Infrastructure/Configuration/`, `Infrastructure/Cost/` | Provider clients, decorators |
| Skills | `Skills/` | `AIFunction` tools exposed to AI models during a request |

### Key Models

- `TaskAnalysis(Domain, Complexity)` — single output of `ITaskAnalyzer`; eliminates the previous double-call bug
- `RoutingDecision(Analysis, Provider, SystemPromptFragment, RequiredSkills)` — flows through the entire pipeline after `AgentSelector.Select()`
- `ProviderDescriptor(Provider, ApiKey, FastModel, CapableModel, Endpoint?)` — one discovered provider

### Domain Agents (`Features/Agents/`)

Plain strategy objects implementing `IDomainAgent`. Each encodes: preferred provider order, system prompt fragment, required skills, and an enhancement hint. `AgentSelector` picks the right agent and resolves the first available preferred provider.

| Agent | Preferred Providers | Skills |
|---|---|---|
| `CodingAgent` | Anthropic → OpenAi | code, memory |
| `ResearchAgent` | Google → OpenAi | search |
| `WritingAgent` | Anthropic → OpenAi | memory |
| `AnalysisAgent` | Anthropic → Google | code |
| `MathAgent` | OpenAi → Google | — |
| `TranslationAgent` | Google → OpenAi | — |
| `ConversationAgent` | Google → OpenAi → Anthropic | — |
| `GeneralAgent` | OpenAi → Google | — |

Adding a new domain = create one class implementing `IDomainAgent` + register in `Program.cs`.

### Provider Discovery (`Infrastructure/Configuration/ProviderRegistry.cs`)

Reads `AI:{Provider}:ApiKey` from config/env at startup. Skips providers with missing or placeholder keys. Creates `IChatClient` instances on demand using the OpenAI-compatible SDK pattern. Exposes `GetNext(current)` for fallback routing.

### Resilience (`Infrastructure/AiProviders/FallbackChatClient.cs`)

`DelegatingChatClient` that catches `HttpRequestException` and provider timeouts. On failure it asks `IProviderRegistry.GetNext()` for the next available provider and retries once. Logs a warning before fallback.

### Task Analysis (`Infrastructure/AiProviders/TaskAnalyzer.cs`)

1. Free heuristic fast-path (length < 25 → Conversation; keyword matching for Translation, Math, greetings)
2. One cheap AI call returning `{"domain":"...","complexity":"..."}` JSON if heuristic is inconclusive

### Skills (`Skills/`)

Skills are only injected when listed in the domain agent's `RequiredSkills`. `MemorySkill` is `AddScoped` — per-request isolation.

| Skill key | Class | Purpose |
|---|---|---|
| `code` | `CodeSkill` | Project structure, file reading, code search |
| `search` | `WebSearchSkill` | Web search (mock) |
| `memory` | `MemorySkill` | Per-request key-value store |
| `time` | `TimeSkill` | UTC / local time |

## ChatRequest Fields

`prompt` (required), `domain?` (TaskDomain override), `complexity?` (ModelComplexity override), `provider?` (AiProvider override), `enablePromptEnhancement` (default true), `useSkills` (default true), `agentId?` (Custom agent override), `sessionId?` (Correlation id), `appId?` (Multi-app override).

When `domain` and `complexity` are both provided, the AI analysis call is skipped entirely.

## ChatResponse Fields

`completion`, `modelUsed`, `providerUsed`, `domain`, `enhancedPrompt?`, `usage?`, `estimatedCost?`, `sessionHit?`, `appId`, `agentScope?`.

## Multi-app & Persistence

The gateway supports multiple apps via the `X-App-Id` header (default: "default").

### Resources & Scoping

- **Memory** (`/api/v1/memory`): Key-value store.
- **Agents** (`/api/v1/agents`): Custom domain agents.
- **Sessions** (`/api/v1/sessions`): Conversational context and routing decision cache.

| Resource | Scope | Sharing |
|---|---|---|
| Memory | `app` (default) \| `global` | Global is read-only for non-owners |
| Agents | `app` (default) \| `global` | Global is read-only for non-owners |
| Sessions | Always `app` | No cross-app sharing |

### Endpoints

- `POST /api/v1/chat/completions`: Chat with session and custom agent support.
- `GET/PUT/DELETE /api/v1/memory/{key}`: Manage persistent memory.
- `POST /api/v1/embeddings`: Generate embeddings with cross-app caching.
- `GET/POST/PUT/DELETE /api/v1/agents`: Manage custom domain agents.
- `GET/DELETE /api/v1/sessions/{id}`: Inspect or end sessions.

## Configuration

API keys via `appsettings.json` or environment variables (env takes precedence):

```
OPENAI_API_KEY      → AI:OpenAi:ApiKey
GOOGLE_API_KEY      → AI:Google:ApiKey
ANTHROPIC_API_KEY   → AI:Anthropic:ApiKey
GATEWAY_API_KEY     → required X-API-Key header on /api/*  (skipped if unset in Development)
GCP_PROJECT_ID      → Firestore project for persistence
```

Default model names are set in `appsettings.json` under `AI:{Provider}:FastModel` / `CapableModel` and can be overridden per environment. See `.env.example` for all available variables.

## Stack

- .NET 10, ASP.NET Core minimal API
- `Microsoft.Extensions.AI` + `Microsoft.Extensions.AI.OpenAI` (v10.5.1) — unified `IChatClient`
- Gemini via OpenAI-compatible endpoint (`generativelanguage.googleapis.com/v1beta/openai/`)
- Anthropic via compatible endpoint (`api.anthropic.com/v1/messages/openai/`)
- C# 10+ records, collection expressions, global usings
