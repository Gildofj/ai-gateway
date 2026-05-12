# Contributing to AI Gateway

First — thank you. AI Gateway is community-driven, and we welcome contributions of all sizes: a typo fix, a new provider integration, a benchmark, or a roadmap discussion.

This document is the **shortest path** from an idea to a merged PR. Skim it once before you start.

---

## Table of contents

- [Code of conduct](#code-of-conduct)
- [Ways to contribute](#ways-to-contribute)
- [Project layout](#project-layout)
- [Development setup](#development-setup)
- [Workflow](#workflow)
- [Coding standards](#coding-standards)
- [Commit message convention](#commit-message-convention)
- [Pull requests](#pull-requests)
- [Adding a provider, agent, or skill](#adding-a-provider-agent-or-skill)
- [Where to ask for help](#where-to-ask-for-help)

---

## Code of conduct

By participating, you agree to uphold our [Code of Conduct](CODE_OF_CONDUCT.md). Report unacceptable behavior privately via the email in that document.

## Ways to contribute

- **Report a bug** — open a [Bug Report issue](../../issues/new?template=bug_report.yml). Include reproduction steps, observed vs. expected behavior, and the model/provider involved.
- **Request a feature** — open a [Feature Request issue](../../issues/new?template=feature_request.yml). Explain the use case and how it fits the routing pipeline.
- **Request a provider** — open a [Provider Request issue](../../issues/new?template=provider_request.yml). Include the provider's API docs and authentication model.
- **Improve docs** — typos, clarifications, new diagrams, and translations are all welcome.
- **Tackle a `good first issue`** — see the [labeled list](../../labels/good%20first%20issue).
- **Discuss design** — open a [GitHub Discussion](../../discussions) before large architectural changes.

## Project layout

```
src/AiGateway.Api/
├─ Core/
│  ├─ Interfaces/    Contracts only — no implementation
│  └─ Models/        Records, enums, value types
├─ Features/
│  ├─ Agents/        Domain agents (CodingAgent, ResearchAgent, ...)
│  ├─ PromptEnhancement/
│  └─ Routing/
├─ Infrastructure/
│  ├─ AiProviders/   DelegatingChatClient decorators
│  ├─ Configuration/ ProviderRegistry — discovers providers from env
│  └─ Cost/          Per-model pricing
├─ Skills/           AIFunction tools (code, search, memory, time)
└─ Program.cs        Composition root + the single endpoint
```

Layer rules (enforced by review):

1. `Core/` has **no** dependencies on the other layers.
2. `Features/` depends on `Core/` only.
3. `Infrastructure/` may depend on `Core/` and ASP.NET / NuGet libraries.
4. `Skills/` is leaf code — no inbound dependencies from other layers.
5. `Program.cs` is the only place where everything is wired together.

## Development setup

### Prerequisites

- **.NET 10 SDK** — [install](https://dotnet.microsoft.com/download/dotnet/10.0)
- **Git**
- At least one provider API key (OpenAI / Anthropic / Google AI Studio)

### Clone, configure, run

```bash
git clone https://github.com/gildofj/ai-gateway.git
cd ai-gateway
cp .env.example .env       # fill in at least one API key
dotnet build src/AiGateway.Api/AiGateway.Api.csproj
dotnet run --project src/AiGateway.Api/AiGateway.Api.csproj
```

For hot reload:

```bash
dotnet watch --project src/AiGateway.Api/AiGateway.Api.csproj
```

Verify the gateway:

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Hello!"}'
```

## Workflow

```
fork → branch → code → build → test → commit → push → PR
```

1. **Fork** the repository and clone your fork.
2. Create a branch from `main`: `git switch -c feat/<short-name>`.
3. Make focused changes. **One concern per PR.**
4. Ensure `dotnet build` produces **zero warnings** before pushing.
5. Open a draft PR early if you'd like feedback. Mark it ready when complete.

## Coding standards

### General

- **C# 10+ idioms**: file-scoped namespaces, target-typed `new()`, collection expressions `[a, b]`, records for value types, primary constructors where they clarify intent.
- **Nullable reference types** are enabled — handle `null` explicitly, never with `!`.
- **Async all the way** — no `.Result` or `.Wait()`.
- **Self-documenting code over comments**. Comment the *why*, not the *what*.
- **Strict layering** — don't reach across layers. If you need to, the design is wrong.

### Build hygiene

- `dotnet build` must succeed with **0 warnings**.
- Don't add a NuGet package unless the standard library and existing dependencies cannot solve the problem.
- Keep the composition root (`Program.cs`) flat and readable.

### Naming

- **Interfaces**: `I<Name>` (`IDomainAgent`, `IPromptEnhancer`)
- **Decorators**: `<Name>Client` extending `DelegatingChatClient`
- **Records**: `Pascal(Field1, Field2)` immutable by default
- **Enums**: singular (`TaskDomain`, `AiProvider`)

### Tests

Test infrastructure is still being set up — see the roadmap. When tests land, prefer:

- **xUnit** for unit tests.
- One assertion per test where practical.
- Mock `IChatClient` for provider behavior; don't hit real APIs in CI.

## Commit message convention

We use [Conventional Commits](https://www.conventionalcommits.org/):

```
<type>(<optional scope>): <description>

[optional body]
[optional footer]
```

Common types: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `ci`.

Examples:

```
feat(routing): add Ollama provider with local model discovery
fix(fallback): retry only on transient HTTP failures
docs(readme): document the cost estimation table
refactor(agents): collapse duplicated PreferredProviders logic
```

Keep the subject line ≤ 72 characters, imperative mood ("add", not "added"), no trailing period.

## Pull requests

A great PR is small, focused, and easy to review.

**Before opening:**

- [ ] Branch name follows `<type>/<short-description>` (e.g. `feat/ollama-provider`).
- [ ] `dotnet build` succeeds with 0 warnings.
- [ ] Manual smoke test against a real provider (or a clear note why it was skipped).
- [ ] Docs updated when behavior, config, or contracts change.
- [ ] `CHANGELOG.md` updated under `## [Unreleased]`.

**PR description should include:**

- **What** changed and **why**.
- **How** to test it (curl example, scenario, etc.).
- **Screenshots / logs** for behavioral changes.
- A link to the issue or discussion it addresses (if any).

PRs are merged by a maintainer once one approval is in, CI is green, and the description matches the diff.

## Adding a provider, agent, or skill

These are the three most common contributions. Each has a step-by-step skill guide in `.agents/skills/` — they are written for the AI assistant but read just as well as a human checklist.

| You want to... | Read |
|---|---|
| Add a new AI provider (e.g. Ollama, Mistral) | [`.agents/skills/add-provider.md`](.agents/skills/add-provider.md) and [`docs/extending/providers.md`](docs/extending/providers.md) |
| Add a new task domain (e.g. Creative) | [`.agents/skills/add-domain-agent.md`](.agents/skills/add-domain-agent.md) and [`docs/extending/domain-agents.md`](docs/extending/domain-agents.md) |
| Add a new skill (e.g. file system) | [`.agents/skills/add-skill.md`](.agents/skills/add-skill.md) and [`docs/extending/skills.md`](docs/extending/skills.md) |
| Debug the pipeline | [`.agents/skills/debug-pipeline.md`](.agents/skills/debug-pipeline.md) |
| Tune token economy | [`.agents/skills/tune-token-economy.md`](.agents/skills/tune-token-economy.md) |

**Rule of thumb:** if you find yourself touching three or more layers in one PR, stop and open a discussion first.

## Where to ask for help

- **Quick question** → [GitHub Discussions](../../discussions/categories/q-a)
- **Design proposal** → [Discussions / Ideas](../../discussions/categories/ideas)
- **Found a bug** → [Issues](../../issues)
- **Security concern** → see [SECURITY.md](SECURITY.md) — **do not** open a public issue

Thanks for being here. 🙏
