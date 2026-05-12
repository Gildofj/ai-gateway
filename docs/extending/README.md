# Extending AI Gateway

Three contribution shapes cover ~90% of changes:

| Add a... | When | Guide |
|---|---|---|
| **Provider** | New AI vendor (Mistral, Ollama, Bedrock, ...) | [`providers.md`](providers.md) |
| **Domain agent** | New `TaskDomain` (e.g. Creative, Legal, Medical) | [`domain-agents.md`](domain-agents.md) |
| **Skill** | New tool the AI can call mid-request | [`skills.md`](skills.md) |

Each guide is a one-pager with a step-by-step checklist. Each one also has a companion file in [`.agents/skills/`](../../.agents/skills/) that the project's AI assistant reads — they double as concise human checklists.

Before contributing, read [`../../CONTRIBUTING.md`](../../CONTRIBUTING.md) for the workflow, coding standards, and commit conventions.
