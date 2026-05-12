# FAQ

## Why does the gateway need an API key just to start?

`TaskAnalyzer` and `PromptEnhancer` both depend on a real `IChatClient` at startup. They use the *fastest model of the first discovered provider* to do their job. If no providers are configured, those services would crash on the first request anyway — so we fail fast at startup with a clear message instead.

## Does the gateway store conversations?

No. The endpoint is **stateless** — each request is independent. `MemorySkill` provides a per-request in-memory dictionary that is discarded as soon as the request completes.

## Can I disable the AI classifier?

Yes. Pass both `domain` and `complexity` in the request body:

```json
{ "prompt": "...", "domain": "Coding", "complexity": "High" }
```

This is documented in [`api-reference.md`](api-reference.md) and saves one round-trip.

## Why is my coding prompt being routed to Conversation?

`TaskAnalyzer` has a heuristic that maps **prompts shorter than 25 characters** to Conversation. Pad your prompt, or pin `domain: "Coding"` explicitly.

## Why does the model name in `modelUsed` differ from what I requested?

Two reasons:

1. **Fallback** — the primary provider failed and the gateway retried on the next one. Check the logs for a `FallbackChatClient` warning.
2. **Complexity routing** — you didn't pin `complexity`, so the gateway picked `FastModel` or `CapableModel` based on its analysis.

`providerUsed` and `modelUsed` in the response always reflect what actually answered.

## How do I add a new provider?

See [`extending/providers.md`](extending/providers.md). Short version:

1. Add the provider key to `AiProvider` enum.
2. Register it in `ProviderRegistry.DiscoverProviders`.
3. Add pricing in `CostTracker`.
4. Update the agent preferred-provider lists if relevant.

## How do I add a new domain?

See [`extending/domain-agents.md`](extending/domain-agents.md). One class implementing `IDomainAgent` plus a single line in `Program.cs`.

## How do I add a new skill?

See [`extending/skills.md`](extending/skills.md). A static class with `[Description]`-decorated methods returning `IEnumerable<AITool>`.

## Why is the build set to "0 warnings"?

Warnings are signals. We treat them the same as errors so they cannot accumulate. Turning the dial up is easy and contagious; turning it back down rarely happens.

## Does the gateway support streaming?

Not yet. The endpoint returns a buffered `200 OK` JSON body. Streaming is on the [roadmap](roadmap.md).

## Does the gateway support Ollama / local models?

Not yet. The `AiProvider.Ollama` enum exists as a reservation. See the [roadmap](roadmap.md).

## How do I run it in production?

- Build a Docker image off `mcr.microsoft.com/dotnet/aspnet:10.0` (sample Dockerfile in [`extending/`](extending/) — coming soon).
- Run behind a reverse proxy that terminates TLS.
- Set provider API keys via your secret manager — not via `.env` in production.
- Configure rate limiting at the proxy layer.
- See the hardening checklist in [`../SECURITY.md`](../SECURITY.md#hardening-recommendations-for-operators).

## Is there a JavaScript / Python SDK?

Not officially. The endpoint is plain JSON over HTTP, so any language with an HTTP client works. SDKs may land once the API stabilizes — see the [roadmap](roadmap.md).

## Where do I get help?

[`../SUPPORT.md`](../SUPPORT.md).

## How can I sponsor / fund the project?

See [`../.github/FUNDING.yml`](../.github/FUNDING.yml). Channels will be enabled once the project crosses 1.0.
