# Roadmap

A living plan for what AI Gateway is working toward. Item order is informational, not a strict sequence. Open questions are flagged so the community can chime in.

## Near-term (pre-1.0)

### 🧱 Stabilize the request contract

- Lock the `/api/v1/chat/completions` request and response shapes.
- Add structured error responses (problem+json or RFC 9457).
- Add request validation with helpful error messages on malformed enums.

### 🌐 More providers

- **Ollama** — local model support. The `AiProvider.Ollama` enum is already reserved.
- **Mistral** via OpenAI-compatible endpoint.
- **Azure OpenAI** — deployment-id awareness.
- **AWS Bedrock** — wider model catalogue under a unified credential.

> Open question: should Bedrock be a single provider or one provider per family (Anthropic-on-Bedrock vs. Mistral-on-Bedrock)? Discussions welcome.

### ⚡ Streaming

- Server-sent events for the `chat/completions` endpoint.
- The `IChatClient` abstraction already exposes a streaming method — the work is at the HTTP layer.

### 🛟 Resilience upgrades

- Distinguish transient (`429`, `503`) from terminal (`401`, `403`) failures.
- Per-provider exponential backoff before fallback.
- Configurable maximum fallback hops (currently fixed at 1).

### 🧠 Better routing

- Multi-armed bandit on latency × cost × success rate per (domain, provider, model).
- A "budget mode" that caps cost per request and biases toward `Low` complexity.
- Domain-aware skill toggles (e.g. enable `code` only when the prompt looks like code).

## Mid-term

### 📦 Caching

- **Semantic cache** keyed by prompt embedding + domain — a hit returns immediately with `cached: true` and `cost: 0`.
- TTL and size policies configurable per domain.

### 🔭 Observability

- OpenTelemetry traces spanning the pipeline.
- Structured request logs (without prompt content by default) with sampling.
- Per-domain / per-provider metrics: success rate, latency p50/p95, cost.

### 🧩 Skills marketplace

- A skills folder pattern that maps to discoverable plugins.
- A way for skills to declare their own configuration and secrets.
- Hot-reload skills in development.

### 🪪 AuthN/AuthZ

- API-key auth in front of the gateway (independent of provider keys).
- Per-tenant quotas and accounting.
- Per-tenant default model overrides.

### 🌍 SDKs

- TypeScript / JavaScript client.
- Python client.
- Generated from the OpenAPI document.

## Long-term

### 🔁 Multi-turn

- Optional server-side conversation persistence with a swappable store (Redis, Postgres).
- Conversation summarization to bound token growth.

### 🧪 Evals

- A bundled benchmark suite that scores every supported provider on each domain.
- Output: a leaderboard maintainers can use to update default `PreferredProviders`.

### 🧬 RAG primitives

- Vector store abstraction (`IVectorStore`).
- Retrieval skill that respects domain agent boundaries.

## Anti-goals

These are explicit non-goals to keep the project focused:

- ❌ Hosting model weights ourselves.
- ❌ Becoming a workflow engine (LangChain-style chains, DAGs).
- ❌ Replacing provider SDKs — we sit on top of them.

## How to influence the roadmap

- Open a [Discussion](../../discussions/categories/ideas) to propose, refine, or object.
- Open an [Issue](../../issues) for concrete bugs or features you want to ship.
- Send a PR — the fastest way to move an item forward.

The roadmap is updated whenever a meaningful change lands. If something here looks stale, please flag it.
