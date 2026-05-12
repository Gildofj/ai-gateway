# Skill: debug-pipeline

Diagnóstico passo-a-passo do pipeline do AI Gateway. Use junto com o agente `pipeline-debugger`.

---

## Passo 0 — Isolar o sintoma

Antes de investigar, categorize o problema:

| Sintoma | Seção a consultar |
|---|---|
| Provider X nunca é selecionado | [Provider Discovery](#provider-discovery) |
| Domain classificado errado | [TaskAnalysis](#taskanalysis) |
| Modelo errado (fast quando deveria ser capable) | [TaskAnalysis](#taskanalysis) |
| Fallback não disparou / disparou errado | [FallbackChatClient](#fallbackchatclient) |
| Skills não sendo chamadas pelo modelo | [Skills](#skills) |
| `estimatedCost` zerado ou errado | [CostTracker](#costtracker) |
| Resposta genérica sem system prompt do agente | [AgentOptimizationClient](#agentoptimizationclient) |
| Request retorna 500 sem mensagem clara | [Provider Discovery](#provider-discovery) + logs |

---

## Provider Discovery

**Arquivo**: `Infrastructure/Configuration/ProviderRegistry.cs`

**Checar em ordem:**

1. A key existe no ambiente?
```bash
# Verificar se a variável de ambiente está definida
echo $OPENAI_API_KEY   # deve ter valor
echo $GOOGLE_API_KEY
echo $ANTHROPIC_API_KEY
```

2. A key não contém "placeholder"?
```csharp
// ProviderRegistry.TryAdd — filtra keys inválidas:
if (string.IsNullOrWhiteSpace(apiKey) || apiKey.Contains("placeholder", ...))
    return;  // provider não será registrado
```

3. Quantos providers estão registrados?
```csharp
// Adicionar log temporário em Program.cs:
var registry = app.Services.GetRequiredService<IProviderRegistry>();
var available = registry.GetAvailable();
logger.LogInformation("Registered providers: {Providers}", 
    string.Join(", ", available.Select(p => p.Provider)));
```

**Causa raiz mais comum**: key configurada em `appsettings.json` mas variável de ambiente não definida (ou vice-versa). A lógica é `config[keyPath] ?? Environment.GetEnvironmentVariable(envVar)` — se ambos forem null, o provider é ignorado.

---

## TaskAnalysis

**Arquivo**: `Infrastructure/AiProviders/TaskAnalyzer.cs`

**Bypass para isolar**: usar `domain` e `complexity` explícitos no request:
```json
POST /api/v1/chat/completions
{
  "prompt": "seu prompt aqui",
  "domain": "Coding",
  "complexity": "High",
  "enablePromptEnhancement": false
}
```
Se funcionar com override e falhar sem, o problema está no `TaskAnalyzer`.

**Checar o heurístico**: o `TryHeuristic` é executado ANTES da AI call. Se o prompt tem < 25 chars ou contém keywords de greetings/translation/math, o resultado é determinístico — não depende de AI.

**Checar o AI path**: se o heurístico retorna `null`, a AI é chamada com este system prompt:
```
"domain": one of General|Coding|Research|Writing|Analysis|Math|Translation|Conversation
"complexity": one of Low|High
```
A resposta deve ser JSON puro. Se o modelo retornar markdown code fences (` ```json ... ``` `), `ParseAiResponse` faz o trim — verificar se o JSON resultante é válido.

**Adicionar log diagnóstico temporário:**
```csharp
// Em TaskAnalyzer.AnalyzeAsync:
var heuristic = TryHeuristic(prompt);
logger.LogDebug("Heuristic result for '{Prompt}': {Result}", 
    prompt[..Math.Min(50, prompt.Length)], heuristic?.ToString() ?? "null (using AI)");
```

---

## FallbackChatClient

**Arquivo**: `Infrastructure/AiProviders/FallbackChatClient.cs`

**Fallback dispara apenas para:**
- `HttpRequestException` (timeout, connection refused, 5xx)
- `TaskCanceledException` quando NÃO é cancelamento do usuário (`!cancellationToken.IsCancellationRequested`)

**NÃO dispara para:**
- `401 Unauthorized` — key inválida lança exceção diferente
- `400 Bad Request` — request malformado (ex: model name errado)
- `JsonException` — erro de desserialização da resposta

**Verificar se há fallback disponível:**
```csharp
// FallbackChatClient.TryFallbackAsync:
var next = _registry.GetNext(_currentProvider);
// GetNext retorna o PRIMEIRO provider que não seja o atual
// Se só há 1 provider configurado, next = null → throws InvalidOperationException
```

**Sintoma "fallback dispara mas falha"**: o fallback usa `GetNext()` que retorna qualquer provider diferente do atual, sem considerar se o fallback também está com problema. Verificar se há pelo menos 2 providers com keys válidas.

---

## Skills

**Arquivo**: `Program.cs` → `BuildTools()` + domain agent `RequiredSkills`

**Checklist quando skills não são chamadas:**

1. `useSkills: true` no request? (default é `true`)
2. `decision.RequiredSkills.Count > 0`? (verificar o domain agent)
3. A key da skill no switch-case bate exatamente com o valor em `RequiredSkills`? (case-sensitive)
4. O modelo selecionado suporta tool use? (todos os modelos configurados suportam, mas verificar se endpoint customizado está correto)

**Checklist quando skills são chamadas desnecessariamente:**

1. O `[Description]` do método diz claramente quando NÃO usar?
2. O system prompt fragment do domain agent está induzindo o modelo a usar tools?

**Testar em isolamento:**
```json
{ "prompt": "...", "domain": "Coding", "useSkills": false }
```
Se funcionar sem skills, o problema está na description ou no system prompt.

---

## AgentOptimizationClient

**Arquivo**: `Infrastructure/AiProviders/AgentOptimizationClient.cs`

**Verificar injeção do system prompt:**
- `InjectSystemPrompt` faz merge com system message existente (se houver)
- Se `SystemPromptFragment` do domain agent estiver vazio, a mensagem ainda é inserida (string vazia)
- Verificar se o agent correto foi selecionado pelo `AgentSelector`

**Verificar context pruning:**
- Só ativa se `messages.Count > 10`
- Mantém `systemMessages` intactas + últimas 6 mensagens não-sistema
- Se mensagens importantes estão sendo perdidas: aumentar o threshold em `PruneContext`

---

## CostTracker

**Arquivo**: `Infrastructure/Cost/CostTracker.cs`

**`estimatedCost` retorna `null` quando**: `response.Usage` é null — alguns providers não retornam usage em modo streaming ou em certos endpoints.

**`estimatedCost` parece errado**: verificar se `model` passado para `EstimateCost` bate com um dos padrões no switch. O switch usa `Contains` — se o model name for um alias ou versão não mapeada, cai no caso `_` com preço genérico ($1.00 input / $3.00 output).

```csharp
// Modelos mapeados atualmente:
// gpt-4o-mini, gpt-4o, gemini-2.0-flash, gemini-2.0-pro,
// gemini-1.5-flash, gemini-1.5-pro, haiku, sonnet
// Qualquer outro → fallback $1/$3
```

---

## Sequência de debug recomendada (request → resposta)

```
1. ProviderRegistry.GetAvailable()     → quais providers estão registrados?
2. TaskAnalyzer.AnalyzeAsync()         → qual domain/complexity foi detectado?
3. AgentSelector.Select()              → qual RoutingDecision foi gerada?
4. ModelRouter.GetModelName()          → qual model name está sendo usado?
5. AgentOptimizationClient             → system prompt está sendo injetado?
6. ChatOptions.Tools                   → quantas tools foram injetadas?
7. provider call                       → qual foi a resposta bruta?
8. response.Usage                      → tokens foram retornados pelo provider?
9. CostTracker.EstimateCost()          → custo calculado corretamente?
```

Use `domain`/`complexity`/`provider` explícitos no request para bypasear etapas e isolar a camada problemática.
