# Routing & cost

How the gateway picks a model for each request — and what it costs you.

## The decision in one diagram

```
prompt
  │
  ▼
TaskAnalyzer
  │
  ├── prompt.Length < 25 ───────────► Conversation / Low
  ├── matches greeting kw ──────────► Conversation / Low
  ├── matches translate kw ─────────► Translation / Low
  ├── matches math kw ──────────────► Math / High
  └── otherwise: 1 cheap AI call ──► <domain> / <Low|High>
                                              │
                                              ▼
                                      AgentSelector
                                              │
                                              ▼
                                  IDomainAgent.PreferredProviders
                                              │
                                              ▼
                       first available provider (request.provider wins)
                                              │
                                              ▼
                                  FastModel | CapableModel
                                              │
                                              ▼
                                       ProviderRegistry.CreateClient
```

## Domains → preferred providers → skills

| Domain | First-choice provider | Falls back to | System prompt focus | Skills wired |
|---|---|---|---|---|
| **Coding** | Anthropic | OpenAI | Clean idiomatic code, concise prose | `code`, `memory` |
| **Research** | Google | OpenAI | Cite sources, structure findings | `search` |
| **Writing** | Anthropic | OpenAI | Adapt tone, clarity, flow | `memory` |
| **Analysis** | Anthropic | Google | Document/data interpretation | `code` |
| **Math** | OpenAI | Google | Step-by-step, precise notation | — |
| **Translation** | Google | OpenAI | Translation-focused | — |
| **Conversation** | Google | OpenAI → Anthropic | Casual, brief | — |
| **General** | OpenAI | Google | Default helpful assistant | — |

> The exact list lives in `src/AiGateway.Api/Features/Agents/*.cs`. Each agent is one file and one class.

When the preferred provider is not configured, the next one in the list is tried. When **none** of the preferred providers is configured, the first discovered provider is used.

## Complexity → model

Each provider declares two slots:

| Complexity | Model slot | Default — OpenAI | Default — Anthropic | Default — Google |
|---|---|---|---|---|
| `Low` | `FastModel` | `gpt-4o-mini` | `claude-3-haiku-20240307` | `gemini-2.0-flash` |
| `High` | `CapableModel` | `gpt-4o` | `claude-3-5-sonnet-20241022` | `gemini-2.0-pro` |

The defaults are in [`appsettings.json`](../src/AiGateway.Api/appsettings.json) and can be overridden by env vars — see [Configuration](configuration.md#model-selection).

## How analysis stays cheap

There are three paths:

1. **Free heuristic.** ~50% of prompts match a length or keyword rule and bypass AI entirely.
2. **Single cheap AI call.** When the heuristic is inconclusive, the analyzer uses the **fastest model of the first discovered provider** to return a tiny JSON object. This call usually costs a fraction of a cent.
3. **Zero analysis.** When the client passes `domain` **and** `complexity`, the gateway skips the analyzer call (see `Program.cs:53`).

If you're sending bulk traffic where you already know the domain, always pin both fields.

## Fallback behavior

`FallbackChatClient` triggers when the primary provider throws:

- `HttpRequestException` — DNS, TLS, 5xx, network unreachable.
- `TaskCanceledException` **not** caused by the caller's cancellation token — a provider timeout.

It logs a warning, asks `IProviderRegistry.GetNext()` for the next provider, and retries **once**. Only one retry — we don't want to fan out to every provider on every failed request.

If the fallback also fails, the exception bubbles to the caller as a `500`.

> Rate-limit errors (`429`) currently surface as `HttpRequestException` and trigger the same fallback. A dedicated backoff strategy is planned.

## Cost estimation

`CostTracker` reads the model id and matches against a substring table (USD per 1M tokens):

| Model substring | Input | Output |
|---|---:|---:|
| `gpt-4o-mini` | $0.15 | $0.60 |
| `gpt-4o` | $5.00 | $15.00 |
| `gemini-2.0-flash` | $0.10 | $0.40 |
| `gemini-2.0-pro` | $3.50 | $10.50 |
| `gemini-1.5-flash` | $0.075 | $0.30 |
| `gemini-1.5-pro` | $3.50 | $10.50 |
| `haiku` | $0.25 | $1.25 |
| `sonnet` | $3.00 | $15.00 |
| _default_ | $1.00 | $3.00 |

The estimate is returned in every response under `estimatedCost`. It is **a snapshot of the prices on the day the table was updated** — always reconcile against the provider's invoice for billing.

## Tuning tips

- **Most "questions" are Low.** If you see expensive models being picked for chit-chat, tighten the heuristic in `TaskAnalyzer` rather than the LLM classifier.
- **Pin the route for known-shape traffic.** Background jobs, scheduled tasks, and internal tools usually know the domain already.
- **Disable prompt enhancement** (`enablePromptEnhancement: false`) for high-volume short prompts — it costs one extra small call per request.
- **Skills cost tokens too.** Each tool registered with the request adds tokens to the request schema. Don't enable skills you don't need (`useSkills: false`).
- **Keep `PreferredProviders` honest.** Order them by quality on that domain, not by personal preference — the first available one wins.

For deeper tuning, see [`.agents/skills/tune-token-economy.md`](../.agents/skills/tune-token-economy.md).
