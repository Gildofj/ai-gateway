# Extending: add a new skill

Skills are tools the AI model can call during a request. They're built on top of `AIFunction` from `Microsoft.Extensions.AI`. AI Gateway ships four: `code`, `search`, `memory`, `time`.

> Companion checklist for AI assistants: [`.agents/skills/add-skill.md`](../../.agents/skills/add-skill.md).

## How skills are wired

1. A domain agent declares `RequiredSkills` (e.g. `["code", "memory"]`).
2. The endpoint reads that list and calls `BuildTools(...)` in `Program.cs`.
3. `BuildTools` returns `AITool[]` from each skill's `GetTools()` factory.
4. The model can call those tools during the response.

Skills are only injected when:

- The selected agent lists them, **and**
- The request didn't set `useSkills: false`.

## Choose: static or scoped?

| Style | When | Example |
|---|---|---|
| **Static class** | Stateless, idempotent, safe to share across requests. | `TimeSkill`, `CodeSkill`, `WebSearchSkill` |
| **Instance class** | Per-request state (e.g. session memory). Registered with `AddScoped`. | `MemorySkill` |

If in doubt, start static. Promote to scoped only when you have state that must not leak between requests.

## 1. Pick a skill key

The key is the string the agent puts in `RequiredSkills`. Keep it short and lowercase: `"code"`, `"search"`, `"files"`, `"db"`.

## 2. Write the skill — static example

`src/AiGateway.Api/Skills/MathSkill.cs`

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class MathSkill
{
    [Description("Evaluates a simple arithmetic expression and returns the numeric result.")]
    public static string Evaluate(
        [Description("A math expression like '12 * (3 + 4)'")] string expression)
    {
        try
        {
            var dt = new System.Data.DataTable();
            var result = dt.Compute(expression, null);
            return result?.ToString() ?? "null";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public static IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(Evaluate),
    ];
}
```

Rules:

- Every public method exposed as a tool needs a `[Description]` attribute.
- Every parameter needs `[Description]` too — the model reads them to decide when to call the tool.
- Keep return types **string** when possible. Models handle text best.
- **Never throw** out of a tool. Return an error string instead — exceptions abort the model's plan.
- Keep the surface small. One skill = a focused capability, not a junk drawer.

## 3. Write the skill — scoped example

`src/AiGateway.Api/Skills/FilesSkill.cs`

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public class FilesSkill
{
    private readonly HashSet<string> _opened = new();

    [Description("Records that the user is working with a file. Useful for follow-up questions.")]
    public string Touch([Description("Absolute or relative file path")] string path)
    {
        _opened.Add(path);
        return $"Tracking '{path}'. {_opened.Count} file(s) in session.";
    }

    [Description("Lists files the user touched during this session.")]
    public string ListTouched() =>
        _opened.Count == 0 ? "No files tracked." : string.Join(", ", _opened);

    public IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(Touch),
        AIFunctionFactory.Create(ListTouched),
    ];
}
```

Register it as scoped:

```csharp
builder.Services.AddScoped<FilesSkill>();
```

## 4. Wire it into the endpoint

`src/AiGateway.Api/Program.cs` — extend `BuildTools` with the new key:

```csharp
static List<AITool> BuildTools(
    IReadOnlyList<string> requiredSkills,
    MemorySkill memorySkill,
    FilesSkill filesSkill)
{
    var tools = new List<AITool>();
    foreach (var skill in requiredSkills)
    {
        switch (skill)
        {
            case "code":   tools.AddRange(CodeSkill.GetTools()); break;
            case "search": tools.AddRange(WebSearchSkill.GetTools()); break;
            case "memory": tools.AddRange(memorySkill.GetTools()); break;
            case "time":   tools.AddRange(TimeSkill.GetTools()); break;
            case "math":   tools.AddRange(MathSkill.GetTools()); break;
            case "files":  tools.AddRange(filesSkill.GetTools()); break;
        }
    }
    return tools;
}
```

And inject the scoped skill into the request handler signature:

```csharp
app.MapPost("/api/v1/chat/completions", async (
    ChatRequest request,
    ITaskAnalyzer analyzer,
    AgentSelector agentSelector,
    IModelRouter router,
    IPromptEnhancer enhancer,
    ICostTracker costTracker,
    MemorySkill memorySkill,
    FilesSkill filesSkill,                       // ← new
    CancellationToken cancellationToken) =>
{ ... });
```

## 5. Opt the right agents in

Find the agents that should have access:

```csharp
// CodingAgent.cs
public IReadOnlyList<string> RequiredSkills => ["code", "memory", "files"];
```

Only opt agents in if the skill is genuinely useful to that domain — skills cost tokens on every request that triggers them.

## 6. Smoke test

```bash
curl -X POST http://localhost:5042/api/v1/chat/completions \
  -H "Content-Type: application/json" \
  -d '{
    "prompt": "Evaluate (12 * 7) / 3 and explain.",
    "domain": "Math",
    "complexity": "Low"
  }'
```

Look for the model invoking the tool in the response (or in your logs depending on log level).

## 7. Document

- Add an entry to the Skills table in [`../../CLAUDE.md`](../../CLAUDE.md).
- Add an entry under **Skills** in the README feature list if it's a notable capability.
- Add an entry in `CHANGELOG.md` under `## [Unreleased]` → `### Added`.

## Security checklist

Skills can do real things on the host. Before merging, confirm:

- [ ] No skill executes arbitrary shell commands or evaluates arbitrary code on user input without sanitization.
- [ ] File-system skills validate paths against a configurable root.
- [ ] Network skills enforce allow-lists.
- [ ] Errors are returned as strings — no stack traces leaked.
- [ ] Skills do not log secrets or user prompts.

## Skill design checklist

- [ ] Methods are **idempotent** where possible.
- [ ] Parameters have `[Description]` attributes.
- [ ] Errors are returned, not thrown.
- [ ] Static unless per-request state is needed.
- [ ] `GetTools()` factory present.
- [ ] Wired in `Program.cs::BuildTools`.
- [ ] Only opted into agents that actually benefit.
- [ ] Smoke test calling the skill from a real request.
- [ ] Build is **0 warnings**.
