# Extending: add a new domain agent

Domain agents encode *what kind of help the user is asking for* and *how the gateway should respond*. Adding one is the cheapest way to specialize the gateway for a new task type.

> Companion checklist for AI assistants: [`.agents/skills/add-domain-agent.md`](../../.agents/skills/add-domain-agent.md).

## What a domain agent is

A plain strategy object implementing `IDomainAgent`. It declares:

| Property | Meaning |
|---|---|
| `Domain` | The `TaskDomain` enum value this agent answers for. |
| `PreferredProviders` | Ordered list — first available wins. |
| `SystemPromptFragment` | Injected ahead of any caller-supplied system message. |
| `RequiredSkills` | Skill keys (`"code"`, `"search"`, `"memory"`, `"time"`) wired as tools. |
| `EnhancementHint` | Hint passed to `PromptEnhancer` to steer the rewrite. |

That's it — no DI gymnastics, no per-agent service.

## 1. Add the domain to the enum

`src/AiGateway.Api/Core/Models/TaskDomain.cs`

```csharp
public enum TaskDomain
{
    General,
    Coding,
    Research,
    Writing,
    Analysis,
    Math,
    Translation,
    Conversation,
    Creative,   // ← new
}
```

> Append at the end — values are persisted in responses.

## 2. Write the agent

`src/AiGateway.Api/Features/Agents/CreativeAgent.cs`

```csharp
using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class CreativeAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.Creative;

    public IReadOnlyList<AiProvider> PreferredProviders =>
        [AiProvider.Anthropic, AiProvider.OpenAi];

    public string SystemPromptFragment =>
        "You are a creative collaborator. Take risks, propose multiple angles, " +
        "and lean into vivid imagery. Avoid clichés. Be brief unless asked otherwise.";

    public IReadOnlyList<string> RequiredSkills => ["memory"];

    public string EnhancementHint =>
        "Clarify genre, audience, length, tone, and any forbidden elements.";
}
```

Notes:

- The system prompt is **prepended** to whatever the caller sends. Keep it short and unambiguous.
- The enhancement hint is the only signal `PromptEnhancer` gets — it should be one tight sentence.
- Only list skills the agent actually needs. Each skill adds tool tokens to every request.

## 3. Update `TaskAnalyzer`

`src/AiGateway.Api/Infrastructure/AiProviders/TaskAnalyzer.cs`

If your domain can be detected cheaply, add a heuristic:

```csharp
if (ContainsAny(lower, "poem", "haiku", "story", "lyric", "verse"))
    return new TaskAnalysis(TaskDomain.Creative, ModelComplexity.Low);
```

Also update the system prompt the analyzer uses, so the AI fallback knows about the new domain:

```csharp
"  \"domain\": one of General|Coding|Research|Writing|Analysis|Math|Translation|Conversation|Creative\n"
```

…and add a routing rule:

```text
Poetry/fiction/short scripts → domain=Creative, complexity=Low or High based on length
```

## 4. Register in `Program.cs`

`src/AiGateway.Api/Program.cs`

```csharp
builder.Services.AddSingleton<IDomainAgent, CreativeAgent>();
```

Order doesn't matter — `AgentSelector` builds a dictionary keyed by `Domain`.

## 5. Smoke test

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Write a 3-line haiku about debugging at 2am.",
    "domain": "Creative",
    "complexity": "Low"
  }'
```

Verify:

- `domain` in the response is `"Creative"`.
- `providerUsed` matches your first preferred provider that is configured.
- Completion reflects the system prompt's tone.

Run a second test without pinning the domain to verify your heuristic kicks in:

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{"prompt":"Write a poem about cold pizza."}'
```

## 6. Document

- Add the agent to the routing table in [`../routing-and-cost.md`](../routing-and-cost.md).
- Add the agent to the table in [`../../CLAUDE.md`](../../CLAUDE.md).
- Add the agent to the agent table in the README.
- Add an entry in `CHANGELOG.md` under `## [Unreleased]` → `### Added`.

## Checklist

- [ ] New enum value appended.
- [ ] `IDomainAgent` implementation in `Features/Agents/`.
- [ ] `TaskAnalyzer` heuristic + AI prompt updated.
- [ ] Singleton registered in `Program.cs`.
- [ ] Smoke test with pinned `domain`.
- [ ] Smoke test with unpinned prompt (heuristic path).
- [ ] Routing table, README, and changelog updated.
- [ ] Build is **0 warnings**.

## Common pitfalls

- **Skipping the analyzer update.** If you don't add the new domain to the analyzer's system prompt, the AI fallback will never return it — only the heuristic will.
- **Listing too many skills.** Skills are tool schemas baked into every request. Listing `code`, `search`, and `memory` together can balloon the prompt; pick the minimum that lets the agent do its job.
- **Over-specifying the system prompt.** Long instructions reduce the room the model has to follow user requests. Keep `SystemPromptFragment` to two or three sentences.
- **Reordering enum values.** It looks tidy but breaks any client persisting `domain`. Always append.
