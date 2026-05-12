# Security Policy

We take the security of AI Gateway seriously. Thank you for helping keep the project and its users safe.

## Supported versions

AI Gateway is pre-1.0. Only the latest commit on `main` receives security fixes.

| Version | Supported |
|---------|-----------|
| `main`  | ✅ |
| Tagged pre-releases | ⚠️ best-effort |
| Forks | ❌ |

Once a stable line ships, this table will be updated with a clear support window.

## Reporting a vulnerability

**Please do not open a public issue for security problems.**

To report a vulnerability:

1. Email **1gildojunior@gmail.com** with the subject line `[SECURITY] AI Gateway — <short summary>`.
2. Include enough detail to reproduce the issue:
   - The affected component (file path / endpoint / decorator).
   - The conditions required (configuration, provider, request body).
   - The observed impact (e.g. key leakage, request smuggling, DoS, prompt injection effect).
   - Optional: a proof-of-concept and a suggested patch.

You will receive an acknowledgement within **72 hours**. We will work with you on a fix and a coordinated disclosure timeline.

If you prefer encrypted communication, mention it in your initial email and we'll exchange a public key.

## Disclosure policy

- We aim to confirm or reject a report within **5 business days**.
- We aim to ship a fix within **30 days** of confirmation for high-severity issues.
- We will credit reporters (with their permission) in the release notes and in [`docs/security/hall-of-thanks.md`](docs/security/hall-of-thanks.md) once it exists.
- We follow a **coordinated disclosure** model: please give us a reasonable window to patch before going public.

## Scope

In scope:

- The HTTP API surface (`/api/v1/*`)
- The configuration pipeline (env vars, `appsettings.json`)
- The provider clients and `DelegatingChatClient` decorators
- Skills (`AIFunction` tools) and any file/system access they perform
- Prompt-handling code paths where user input reaches AI providers

Out of scope:

- Vulnerabilities in upstream provider APIs (OpenAI, Anthropic, Google) — report directly to them.
- Issues that require a compromised host or already-elevated local privileges.
- Findings against demo deployments, forks, or unmaintained branches.

## Handling secrets

AI Gateway reads provider API keys from environment variables (`OPENAI_API_KEY`, `ANTHROPIC_API_KEY`, `GOOGLE_API_KEY`) or `appsettings.json`. **Never commit a real key to the repository.** If you suspect a key has been leaked:

1. Rotate the key immediately at the provider's dashboard.
2. Open a private report following the process above.
3. The maintainers will scrub the repository history if needed.

## Prompt injection

Prompt injection is a known class of issues for LLM applications. AI Gateway minimizes blast radius by:

- Injecting only the **system prompt fragment** of the selected agent — not arbitrary text from previous requests.
- Exposing skills **only when an agent explicitly lists them** in `RequiredSkills`.
- Scoping `MemorySkill` per request (`AddScoped`).

If you discover a prompt-injection path that allows a request to escalate skills it should not have, bypass routing decisions, or exfiltrate keys or other requests' data, please report it.

## Hardening recommendations for operators

If you deploy AI Gateway:

- Run behind an authenticated reverse proxy or API gateway.
- Restrict outbound network access to the providers you actually use.
- Set per-request and per-tenant rate limits.
- Log without persisting full prompts unless you have a compliance need to do so.
- Rotate provider keys on a schedule and on every personnel change.

---

Thank you for making AI Gateway safer.
