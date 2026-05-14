# Architecture

AI Gateway is a small, deliberate codebase. This document is the map.

## Goals & non-goals

**Goals**

- One endpoint, many providers, smart per-request routing.
- Layered design where each piece is replaceable.
- Cheap defaults: heuristics before AI, fast model for analysis, free fallback on transport failure.
- Zero-warning build.

**Non-goals (today)**

- Multi-turn conversation persistence — the gateway is stateless.
- A streaming API — the current endpoint is buffered.
- Vector stores or RAG plumbing — out of scope.

## Layers

```
src/AiGateway.Api/
├── Core/                ← contracts only (interfaces + value types)
│   ├── Interfaces/
│   └── Models/
├── Features/            ← application logic
│   ├── Agents/          ← domain agents implementing IDomainAgent
│   ├── PromptEnhancement/
│   └── Routing/
├── Infrastructure/      ← outside-world adapters
│   ├── AiProviders/     ← IChatClient + DelegatingChatClient decorators
│   ├── Configuration/   ← ProviderRegistry
│   └── Cost/            ← CostTracker
├── Skills/              ← AIFunction tools
└── Program.cs           ← composition root
```

**Dependency rules:**

- `Core` depends on nothing else in the project.
- `Features` depends on `Core` only.
- `Infrastructure` depends on `Core` (+ ASP.NET / NuGet).
- `Skills` is leaf code.
- `Program.cs` is the only place where everything is glued.

These rules are enforced by code review. If a PR violates them, the design needs to change — not the rule.

## Request pipeline

```
                ┌──────────────────────────────────────────────────────┐
HTTP POST ──▶   │ 1. TaskAnalyzer        prompt → TaskAnalysis         │
                │    (heuristic, then 1 cheap AI call if needed)       │
                ├──────────────────────────────────────────────────────┤
                │ 2. AgentSelector       analysis + override → Routing │
                │    Decision { provider, system prompt, skills }      │
                ├──────────────────────────────────────────────────────┤
                │ 3. PromptEnhancer      (optional) rewrites prompt    │
                ├──────────────────────────────────────────────────────┤
                │ 4. ModelRouter         picks IChatClient, wraps in   │
                │    FallbackChatClient                                │
                ├──────────────────────────────────────────────────────┤
                │ 5. AgentOptimizationClient injects system prompt and │
                │    prunes context to system + last 6 messages        │
                ├──────────────────────────────────────────────────────┤
                │ 6. Skills wired as ChatOptions.Tools                 │
                ├──────────────────────────────────────────────────────┤
                │ 7. Provider call → response → CostTracker            │
                └──────────────────────────────────────────────────────┘
                                          │
                                          ▼
                                   ChatResponse JSON
```

Each numbered step maps to a function call you can read in [`Program.cs`](../src/AiGateway.Api/Program.cs).

## Key contracts

All contracts live in `Core/`. Implementations live in `Features/` or `Infrastructure/`.

| Interface | Implementation | Lifetime |
|---|---|---|
| `IProviderRegistry` | `ProviderRegistry` | Singleton |
| `ITaskAnalyzer` | `TaskAnalyzer` | Singleton |
| `IDomainAgent` | `CodingAgent`, `ResearchAgent`, ... | Singleton (one per domain) |
| `IModelRouter` | `ModelRouter` | Singleton |
| `IPromptEnhancer` | `PromptEnhancer` | Singleton |
| `ICostTracker` | `CostTracker` | Singleton |
| — | `MemorySkill` | **Scoped** (per request) |

The single scoped service is `MemorySkill` — every request gets a fresh in-memory dictionary, so requests cannot read each other's state.

## Value types

All records live in `Core/Models/`.

```csharp
public record TaskAnalysis(TaskDomain Domain, ModelComplexity Complexity);

public record RoutingDecision(
    TaskAnalysis Analysis,
    AiProvider Provider,
    string SystemPromptFragment,
    IReadOnlyList<string> RequiredSkills);

public record ProviderDescriptor(
    AiProvider Provider,
    string ApiKey,
    string FastModel,
    string CapableModel,
    Uri? Endpoint = null);
```

`RoutingDecision` is the contract that flows from `AgentSelector.Select()` through every downstream component. Don't bypass it — never pass a raw `ChatRequest` to infrastructure.

## Decorators

Each decorator extends `DelegatingChatClient` from `Microsoft.Extensions.AI`. They compose at the composition root.

| Decorator | Responsibility |
|---|---|
| `FallbackChatClient` | Catches `HttpRequestException` and timeouts → retries on `IProviderRegistry.GetNext()`. Logs a warning before fallback. |
| `AgentOptimizationClient` | Injects the domain system prompt; merges any existing system message; prunes context to system + last 6 messages. |
| `ProviderOptimizationClient` | Per-provider tweaks (e.g. concise hint for Anthropic, tool usage hint for Gemini). Wired inside `ProviderRegistry.CreateClient`. |

Order matters. Reading from inside out:

```
provider call
└── ProviderOptimizationClient    (added inside ProviderRegistry.CreateClient)
    └── FallbackChatClient         (added in ModelRouter.GetClient)
        └── AgentOptimizationClient (added in Program.cs after router)
            └── caller
```

The agent system prompt is injected **last** so it sits closest to the caller, and the fallback wrapper sees the request before per-provider hints kick in.

## Provider discovery

`ProviderRegistry.DiscoverProviders` is called once at startup. For each candidate provider it:

1. Reads the configured API key path (`AI:<Provider>:ApiKey`).
2. Falls back to the env var (`OPENAI_API_KEY`, etc).
3. Skips if missing, empty, or contains `placeholder`.
4. Records `FastModel`, `CapableModel`, and an optional OpenAI-compatible endpoint.

The registry exposes:

- `GetAvailable()` — current list of discovered providers.
- `IsAvailable(AiProvider)` — used by `AgentSelector` to filter preferred providers.
- `CreateClient(provider, complexity)` — builds an `IChatClient` already wrapped in `ProviderOptimizationClient`.
- `GetNext(current)` — first provider other than `current`; used by `FallbackChatClient`.

## Task analysis (cheap by design)

`TaskAnalyzer.AnalyzeAsync`:

1. **Heuristic pass** (free): `prompt.Length < 25` → Conversation/Low; keyword matches on greetings, translation, math.
2. **AI pass** (one call, fastest model of the first available provider): returns `{ "domain": "...", "complexity": "..." }` JSON.

When the request specifies both `domain` and `complexity`, **the whole analyzer is skipped** — see `Program.cs:53`. This is the cheapest hot path.

## Agent selection

`AgentSelector.Select`:

1. Look up the agent for the analyzed domain. Default to `GeneralAgent` if missing.
2. If `request.Provider` is set, use it; otherwise pick the first preferred provider that is actually available.
3. Return a `RoutingDecision` carrying the analysis, provider, system prompt, and skill list.

`AgentSelector.GetEnhancementHint(domain)` is read separately by `PromptEnhancer` — keeping enhancement-time data colocated with the agent.

## Cost tracking

`CostTracker.EstimateCost(model, inputTokens, outputTokens)` returns USD using a per-million-tokens table. Add new pricing by editing the `switch` — pricing snapshots live with the code so they're easy to audit and update.

## What's intentionally simple

- **No DI scopes inside the request handler** beyond `MemorySkill`. The pipeline is a straight sequence of method calls — easier to read than a chain of middlewares.
- **No reflection-driven plugin discovery.** Domain agents and skills are registered explicitly in `Program.cs`.
- **No streaming yet.** The endpoint returns a buffered `200 OK` JSON body.
