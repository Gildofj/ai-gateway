# Internal Architecture: Patterns & Decisions

This guide explains the "why" behind AI Gateway's implementation details. If you're a contributor or just curious about the internals, start here.

## 1. Pipeline-as-a-Function

Unlike typical ASP.NET Core apps that use a chain of `Middleware`, AI Gateway's request pipeline is a **single, flat sequence of function calls** in `Program.cs`.

**Why?**
- **Traceability**: You can "F12" from the endpoint entry point and see the entire flow without jumping through `Configure()` or DI registrations.
- **Explicit state**: Data flows through a typed `RoutingDecision` record. There is no `HttpContext.Items` or "magic" shared state.

## 2. Decorators via `DelegatingChatClient`

AI Gateway uses the decorator pattern to add behavior to the AI calls. This is built on top of the `Microsoft.Extensions.AI` abstraction.

**The Chain:**
1. **`ProviderOptimizationClient`**: The inner-most layer. Adds provider-specific hints (e.g., "be concise" for Anthropic) right before the byte-stream goes to the API.
2. **`FallbackChatClient`**: Wraps the inner call in a `try-catch`. If it catches a transport error, it asks the registry for a different provider and tries once more.
3. **`AgentOptimizationClient`**: The outer layer. Injects the domain's system prompt and prunes the message history to keep the context window clean.

**Why?**
- **Separation of Concerns**: The `FallbackChatClient` doesn't know about system prompts; `AgentOptimizationClient` doesn't know about HTTP retries.
- **Composition**: In `ModelRouter.cs`, we compose these into a single `IChatClient` that looks like a single provider to the rest of the app.

## 3. Heuristic-First Analysis

Before making a "smart" AI call to classify a prompt, `TaskAnalyzer` runs a series of regex and keyword checks.

**Why?**
- **Latency**: A regex check takes microseconds; an LLM call takes 500ms+.
- **Cost**: Heuristics are free. 
- **Predictability**: Simple greetings or math expressions are better handled by rules than by a probabilistic model that might hallucinate a domain.

## 4. Statelessness & Scoped Skills

The gateway is stateless. It does not remember previous requests unless the client passes them back in the `messages` array (standard OpenAI pattern).

However, `MemorySkill` provides a "session memory" *within a single request*.

**Why?**
- **Security**: By registering `MemorySkill` as `AddScoped`, ASP.NET Core ensures every request gets its own instance. There is zero risk of one user's tools seeing data from another user's request.
- **Simplicity**: No need for Redis or a database to support short-term tool coordination.

## 5. Pricing-as-Code

Token pricing lives in `CostTracker.cs` as a switch-expression.

**Why?**
- **Auditability**: Pricing changes are visible in git history.
- **Performance**: A memory lookup is faster than a DB query or a cache hit.
- **Simplicity**: We don't need a "Pricing Service" for a project of this scale.

## 6. Terraform Adoption Pattern

The `infra/terraform` folder includes an `imports.tf` file.

**Why?**
- **Brownfield-friendly**: Most developers bootstrap projects manually in the GCP console first. `imports.tf` provides a paved path to move from "manual clicking" to "IaC" without recreating resources and losing data (or changing IDs).

---

For the "what" and "how," see [Architecture](architecture.md).
