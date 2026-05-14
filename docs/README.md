# AI Gateway documentation

Welcome. This folder is the canonical home for AI Gateway documentation. It is organized for three audiences:

| Audience | Start here |
|---|---|
| **Operator / user** — wants to run the gateway and call it | [`getting-started.md`](getting-started.md) → [`configuration.md`](configuration.md) → [`api-reference.md`](api-reference.md) |
| **Contributor** — wants to understand and extend the code | [`architecture.md`](architecture.md) → [`internal-architecture.md`](internal-architecture.md) → [`routing-and-cost.md`](routing-and-cost.md) → [`extending/`](extending/) |
| **Maintainer** — wants governance and standards | [`../CONTRIBUTING.md`](../CONTRIBUTING.md) → [`../SECURITY.md`](../SECURITY.md) → [`../infra/terraform/README.md`](../infra/terraform/README.md) |

## Table of contents

### Using AI Gateway

- [Getting started](getting-started.md)
- [Configuration](configuration.md)
- [Deployment](deployment.md)
- [API reference](api-reference.md)
- [Routing & cost](routing-and-cost.md)
- [FAQ](faq.md)

### Extending AI Gateway

- [Add a provider](extending/providers.md)
- [Add a domain agent](extending/domain-agents.md)
- [Add a skill](extending/skills.md)

### Project & Infrastructure

- [Architecture (High-level)](architecture.md)
- [Architecture (Internal & Patterns)](internal-architecture.md)
- [Infrastructure (Terraform)](../infra/terraform/README.md)

## Conventions

- Code blocks are tagged with the runtime language (`bash`, `csharp`, `json`, `http`).
- File paths are relative to the repository root unless otherwise noted.
- Diagrams use ASCII whenever possible to stay reviewable in PRs. Mermaid is fine for complex flows; keep it inline so it renders on GitHub.
- Examples target a local gateway at `http://localhost:5042`.

If you find a stale doc, please open a PR. Docs decay faster than code — every fix counts.
