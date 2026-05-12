# Skill: add-domain-agent

Procedimento para adicionar um novo agente de domínio ao gateway. Exatamente 3 arquivos (4 se o domínio for novo).

---

## Passo 0 (condicional) — `Core/Models/TaskDomain.cs`

Se o domínio ainda não existe, adicionar ao enum:

```csharp
public enum TaskDomain
{
    General, Coding, Research, Writing, Analysis, Math, Translation, Conversation,
    MyDomain   // <- adicionar aqui
}
```

---

## Passo 1 — `Features/Agents/MyDomainAgent.cs`

```csharp
using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Features.Agents;

public class MyDomainAgent : IDomainAgent
{
    public TaskDomain Domain => TaskDomain.MyDomain;
    public IReadOnlyList<AiProvider> PreferredProviders => [AiProvider.OpenAi, AiProvider.Google];
    public string SystemPromptFragment => "Role. Key behavior. Critical constraint.";
    public IReadOnlyList<string> RequiredSkills => [];
    public string EnhancementHint => "What context is usually implicit for this domain.";
}
```

**Guia para `PreferredProviders`:**
- Coding, Analysis, Writing → Anthropic primeiro (raciocínio/qualidade)
- Research, Translation → Google primeiro (contexto longo, multilingual)
- Math → OpenAI primeiro (melhor em aritmética)
- Conversation → Google primeiro (custo/velocidade)

**Guia para `RequiredSkills`:**
- Precisa de código/arquivos → `"code"`
- Precisa de informação atual → `"search"`
- Precisa de estado entre turns → `"memory"`
- Precisa de hora atual → `"time"`
- Tarefas simples → `[]`

---

## Passo 2 — `Program.cs`

Registrar o agente (junto com os outros `AddSingleton<IDomainAgent>`):

```csharp
builder.Services.AddSingleton<IDomainAgent, MyDomainAgent>();
```

---

## Passo 3 (condicional) — `Infrastructure/AiProviders/TaskAnalyzer.cs`

Se adicionou um novo `TaskDomain`, adicionar keywords ao `TryHeuristic`:

```csharp
if (ContainsAny(lower, "keyword1", "keyword2", "keyword3"))
    return new TaskAnalysis(TaskDomain.MyDomain, ModelComplexity.Low); // ou High
```

Escolher `Low` para tarefas simples/rápidas, `High` para raciocínio/análise profunda.

---

## Verificação

```bash
dotnet build src/AiGateway.Api/AiGateway.Api.csproj
```

Testar com um prompt que deveria classificar no novo domínio:

```json
POST /api/v1/chat/completions
{ "prompt": "...", "enablePromptEnhancement": false }
```

Verificar que `domain` no response bate com o esperado.
