# GEMINI.md

# Mandatos do Projeto (AI Gateway)

Este arquivo define as regras arquiteturais e padrões de engenharia mandatórios para o desenvolvimento do AI Gateway.

## 🏛️ Princípios Arquiteturais

1.  **Isolamento Multi-app (X-App-Id)**:
    *   Toda requisição e recurso deve estar vinculado a um `AppId`.
    *   O isolamento é mandatório por padrão. O fallback é sempre para o appId "default".
    *   Recursos (Memory, Agents) devem ser criados no escopo `app`, a menos que o escopo `global` seja explicitamente solicitado.

2.  **Persistência Stateful via Firestore**:
    *   O gateway não é mais puramente stateless. O Firestore é a fonte da verdade para Memória, Agentes Customizados e Sessões.
    *   Layout de Coleções:
        *   App-scoped: `apps/{appId}/{collection}/{id}`
        *   Global-scoped: `shared/global/{collection}/{id}` (read-only para não-owners).
        *   Embeddings Cache: `shared/global/embeddings_cache/{hash}` (cross-app dedup).

3.  **Correlação de Sessão**:
    *   Sempre que um `sessionId` for fornecido, o gateway deve persistir e recuperar o histórico conversacional (`turns`) e reusar decisões de roteamento anteriores (cache de `domain` e `complexity`).

## 🛠️ Padrões de Engenharia

1.  **Pipeline Async**:
    *   Todo o pipeline de chat e stores de persistência devem ser 100% assíncronos.
    *   `AgentSelector.SelectAsync` é o ponto central de resolução de agentes (Fallback: Custom App -> Custom Global -> Built-in).

2.  **Eficiência de Tokens & Custos**:
    *   **Task Analyzer Bypass**: Se `domain` e `complexity` forem fornecidos no request ou encontrados no cache da sessão, o `ITaskAnalyzer` (AI call) deve ser pulado.
    *   **Context Pruning**: Manter o histórico de sessões limitado a 6 turnos para balancear profundidade de contexto e custo.

3.  **Built-in vs Custom Agents**:
    *   Agentes built-in são definidos em código (estratégias compiladas).
    *   Agentes customizados (via API) podem shadowar (sobrescrever) built-ins se usarem o mesmo ID no escopo do app.

## 📂 Estrutura de Camadas (FSD-ish)

*   `Core/`: Contratos e modelos puros.
*   `Features/`: Lógica de aplicação (Chat, Memory, Embeddings, Agents, Sessions).
*   `Infrastructure/`: Implementações de persistência, clientes de providers e decorators.
*   `Skills/`: Ferramentas de runtime (Tools) injetadas no modelo.

## 🚀 Novas Regras de Endpoint

*   **Embeddings**: Devem sempre tentar o cache `shared` antes de chamar o provider.
*   **Memory**: `MemorySkill` deve delegar para `IMemoryStore` para persistência entre turnos e apps.
*   **Security**: O header `X-API-Key` é obrigatório em todos os endpoints `/api/*` em produção.
