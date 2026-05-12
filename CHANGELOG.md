# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog 1.1.0](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning 2.0.0](https://semver.org/spec/v2.0.0.html).

> Until `1.0.0`, breaking changes may occur on minor releases. They will always be flagged in the **Changed** section with a ⚠️ marker and a migration note.

## [Unreleased]

### Added
- Initial open-source documentation set: `README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `SECURITY.md`, `SUPPORT.md`, `AUTHORS`, `CHANGELOG.md`.
- `docs/` guides: architecture, getting started, configuration, API reference, routing & cost, extending (providers, agents, skills), FAQ, roadmap.
- `.github/` community templates: issue templates, pull request template, funding placeholder.

## [0.1.0] — 2026-05-12

### Added
- .NET 10 minimal API exposing `POST /api/v1/chat/completions`.
- `ProviderRegistry` that discovers configured providers (OpenAI, Anthropic, Google) from environment variables and `appsettings.json`.
- `TaskAnalyzer` with a free heuristic fast-path and a single cheap AI call for ambiguous prompts; returns a `TaskAnalysis(Domain, Complexity)`.
- Eight domain agents implementing `IDomainAgent`: `CodingAgent`, `ResearchAgent`, `WritingAgent`, `AnalysisAgent`, `MathAgent`, `TranslationAgent`, `ConversationAgent`, `GeneralAgent`.
- `AgentSelector` that picks the agent and resolves the first available preferred provider.
- `PromptEnhancer` that rewrites prompts using a domain-specific hint when `enablePromptEnhancement` is true.
- `ModelRouter` returning an `IChatClient` wrapped in `FallbackChatClient`.
- `FallbackChatClient` decorator that catches `HttpRequestException` / timeouts and retries on the next available provider.
- `AgentOptimizationClient` decorator that injects the domain system-prompt fragment and prunes context to the last 6 non-system messages.
- `ProviderOptimizationClient` decorator with per-provider tweaks (concise hint for Anthropic, tool guidance for Google).
- Skills exposed as `AIFunction` tools: `CodeSkill`, `WebSearchSkill` (mocked), `MemorySkill` (per-request), `TimeSkill`.
- `CostTracker` returning per-request USD cost estimates from per-million-token pricing.
- `ChatResponse` payload including `completion`, `modelUsed`, `providerUsed`, `domain`, `enhancedPrompt`, token `usage`, and `estimatedCost`.
- OpenAPI document available at `/openapi/v1.json` in development.

[Unreleased]: https://github.com/gildofj/ai-gateway/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/gildofj/ai-gateway/releases/tag/v0.1.0
