# Skill: add-skill

Procedimento para adicionar uma nova AIFunction skill ao gateway. Exatamente 3 arquivos.

---

## Passo 1 — `Skills/MySkill.cs`

### Skill stateless (sem estado entre chamadas)

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public static class MySkill
{
    [Description("One sentence: what it does. When the AI SHOULD call it. What it returns.")]
    public static string DoAction(
        [Description("Format/type/expected values for this param")] string param)
    {
        try
        {
            // lógica aqui
            return result;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    public static IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(DoAction)
    ];
}
```

### Skill stateful (precisa de estado por request)

```csharp
using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Skills;

public class MySkill   // não static
{
    private readonly Dictionary<string, string> _state = new();

    [Description("...")]
    public string DoAction(string param) { ... }

    public IEnumerable<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(DoAction)
    ];
}
```

Registrar como `builder.Services.AddScoped<MySkill>()` e injetar no endpoint lambda.

---

## Passo 2 — `Program.cs` — `BuildTools()`

Adicionar um case ao switch:

```csharp
case "myskill":
    tools.AddRange(MySkill.GetTools());     // stateless
    // ou:
    tools.AddRange(mySkillInstance.GetTools()); // stateful, injetado como parâmetro
    break;
```

A chave string (`"myskill"`) deve ser lowercase e sem espaços.

---

## Passo 3 — Domain agents

Nos agents que devem ter acesso a essa skill, adicionar a chave a `RequiredSkills`:

```csharp
public IReadOnlyList<string> RequiredSkills => ["code", "myskill"];
```

---

## Regras de segurança obrigatórias

- **Filesystem**: sempre usar `Path.GetFullPath()`, nunca navegar para fora do diretório de trabalho
- **Output size**: cap com `.Take(N)` antes de retornar listas (padrão: 50 itens)
- **Exceptions**: capturar e retornar como string de erro — nunca propagar
- **IO assíncrono**: use `async Task<string>` se necessário, funciona com `AIFunctionFactory.Create`

---

## Verificação

```bash
dotnet build src/AiGateway.Api/AiGateway.Api.csproj
```

Testar com `useSkills: true` e um prompt que naturalmente levaria o modelo a usar a nova skill.
