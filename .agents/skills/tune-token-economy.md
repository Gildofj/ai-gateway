# Skill: tune-token-economy

Padrões concretos para reduzir tokens em cada camada do pipeline. Leia junto com o agente `token-economy-tuner`.

---

## Camada 1 — TaskAnalyzer: expandir o fast-path heurístico

**Objetivo**: evitar a chamada AI de classificação. Cada prompt classificado via heurística = zero tokens gastos nessa etapa.

**Onde**: `Infrastructure/AiProviders/TaskAnalyzer.cs` → método `TryHeuristic`

**Padrão de adição de heurística:**
```csharp
// Comprimento curto → sempre Conversation/Low (sem AI call)
if (prompt.Length < 25)
    return new TaskAnalysis(TaskDomain.Conversation, ModelComplexity.Low);

// Novos domínios: adicionar ANTES dos casos mais genéricos
if (ContainsAny(lower, "keyword1", "keyword2"))
    return new TaskAnalysis(TaskDomain.MyDomain, ModelComplexity.Low);
```

**Regras:**
- Keywords devem ser específicas o suficiente para não gerar falsos positivos
- Complexidade `Low` por padrão; usar `High` apenas se o domínio intrinsecamente exige raciocínio profundo (Math, Analysis)
- Greetings e conversas curtas SEMPRE devem cair no fast-path — nunca desperdiçar uma AI call para classificar "oi"

---

## Camada 2 — AgentSelector: RequiredSkills mínimas

**Objetivo**: cada skill em `RequiredSkills` injeta ~200–500 tokens de definição de tool no system prompt.

**Regra de inclusão:**
| Skill | Incluir quando... |
|---|---|
| `code` | O modelo precisa ler arquivos ou buscar código durante a resposta |
| `search` | O modelo precisa de informação atual que não está no training data |
| `memory` | O request é multi-turn E o domínio precisa lembrar dados entre turns |
| `time` | O domínio precisa da hora atual (raro) |

**Anti-padrões:**
- Não incluir `code` em `WritingAgent` — o modelo nunca precisará ler arquivos para escrever texto
- Não incluir `memory` em `MathAgent` — cálculos são stateless
- Não incluir `search` em `CodingAgent` — o modelo deve usar o conhecimento de training, não buscar na web

**Verificar o impacto:**
```csharp
// Em BuildTools() — número de tools que serão injetadas no ChatOptions
chatOptions.Tools = BuildTools(decision.RequiredSkills, memorySkill);
// Cada AITool adiciona sua assinatura ao prompt do modelo
```

---

## Camada 3 — AgentOptimizationClient: context pruning

**Objetivo**: conversas longas acumulam tokens. O cliente poda o histórico mantendo apenas as mensagens mais recentes.

**Onde**: `Infrastructure/AiProviders/AgentOptimizationClient.cs` → `PruneContext`

**Parâmetros atuais:**
- Threshold de ativação: `messages.Count > 10`
- Mensagens não-sistema mantidas: últimas `6`

**Quando ajustar:**
```csharp
// Domínios com contexto longo necessário (ex: Coding com histórico de sessão)
// → aumentar para 8–10 mensagens mantidas

// Domínios stateless (ex: Translation, Math)
// → reduzir threshold para 6, manter apenas 4 mensagens recentes
```

**Invariante**: system messages NUNCA são podadas — elas contêm o system prompt fragment do domain agent.

---

## Camada 4 — PromptEnhancer: custo vs benefício

**Custo**: 1 AI call completa (FastModel) por request com `enablePromptEnhancement: true`.

**Vale o custo:**
| Domínio | Vale? | Motivo |
|---|---|---|
| Coding | Sim | Prompt mal especificado → resposta errada. Custo do retrabalho > custo do enhancement |
| Analysis | Sim | Contexto implícito frequentemente ausente no prompt original |
| Writing | Sim | Tom, audiência, formato raramente especificados pelo usuário |
| Research | Talvez | Útil se o prompt for vago; dispensável se já for uma query clara |
| Math | Não | Prompts matemáticos já são precisos por natureza |
| Translation | Não | "Traduza X para Y" não tem como melhorar |
| Conversation | Não | Destruiria a naturalidade da conversa |

**Para desabilitar por domínio** (ao invés de deixar para o caller):
```csharp
// Em Program.cs, antes do step 3:
if (request.EnablePromptEnhancement && ShouldEnhance(analysis.Domain))
{
    // ...
}

static bool ShouldEnhance(TaskDomain domain) => domain is
    TaskDomain.Coding or TaskDomain.Analysis or TaskDomain.Writing;
```

---

## Camada 5 — ModelComplexity: fast vs capable

**FastModel** (Low): conversas, traduções, classificações simples, perguntas diretas  
**CapableModel** (High): código, análise de documentos, matemática, raciocínio encadeado

**Custo relativo típico** (input tokens por $ 1M):
| Tier | OpenAI | Google | Anthropic |
|---|---|---|---|
| Fast | gpt-4o-mini ($0.15) | gemini-2.0-flash ($0.10) | claude-3-haiku ($0.25) |
| Capable | gpt-4o ($5.00) | gemini-2.0-pro ($3.50) | claude-3.5-sonnet ($3.00) |

**Regra de ouro**: se o `TaskAnalyzer` retorna `High` para uma tarefa trivial, o heurístico está errado — corrija o heurístico, não o threshold de roteamento.

---

## Checklist de revisão antes de qualquer mudança no pipeline

- [ ] A mudança adiciona uma nova AI call? Se sim, há um fast-path para evitá-la?
- [ ] As `RequiredSkills` dos domain agents afetados são mínimas para a tarefa?
- [ ] O context pruning ainda funciona corretamente com a mudança?
- [ ] Prompts curtos (< 25 chars) ainda caem no heurístico sem AI call?
- [ ] `estimatedCost` ainda reflete o custo real após a mudança?
