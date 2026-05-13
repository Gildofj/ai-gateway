# API reference

AI Gateway exposes a single endpoint. This document is the contract.

> The same definition is published as OpenAPI at `GET /openapi/v1.json` when the gateway runs in development.

## `POST /api/v1/chat/completions`

Run a chat completion through the full pipeline: analyze → select agent → enhance → execute → cost.

### Request

`Content-Type: application/json`

| Field | Type | Required | Default | Description |
|---|---|---|---|---|
| `prompt` | string | ✅ | — | The user prompt. |
| `domain` | enum `TaskDomain` | — | inferred | Override the classifier. See [Enums](#enums). |
| `complexity` | enum `ModelComplexity` | — | inferred | Override the classifier. `Low` or `High`. |
| `provider` | enum `AiProvider` | — | agent-selected | Force a provider (`OpenAi`, `Anthropic`, `Google`, `Ollama`). |
| `enablePromptEnhancement` | bool | — | `true` | Run the `PromptEnhancer` step. |
| `useSkills` | bool | — | `true` | Inject the agent's required skills as tools. |
| `systemInstruction` | string | — | — | Extra system prompt appended after the domain agent's system fragment. Use for caller-specific persona or rules. |
| `responseMimeType` | string | — | — | Set to `"application/json"` to enable structured JSON output. |
| `responseSchema` | object (JSON Schema) | — | — | Raw JSON Schema constraining the response. Requires `responseMimeType: "application/json"`. When omitted with JSON mime type, the model returns plain JSON without schema enforcement. |

> **Cost tip:** when both `domain` **and** `complexity` are provided, the gateway skips the AI classification call entirely (one fewer round-trip per request).

#### Minimal example

```http
POST /api/v1/chat/completions
Content-Type: application/json

{
  "prompt": "Translate 'good evening' to Japanese."
}
```

#### Pinned routing example

```http
POST /api/v1/chat/completions
Content-Type: application/json

{
  "prompt": "Implement a debounce function in TypeScript.",
  "domain": "Coding",
  "complexity": "High",
  "provider": "Anthropic",
  "enablePromptEnhancement": false,
  "useSkills": true
}
```

#### Structured JSON output

Use `responseMimeType` and `responseSchema` to force the model to return a JSON document matching a schema you define. `completion` will be a JSON-stringified value that the caller can parse safely.

```http
POST /api/v1/chat/completions
Content-Type: application/json

{
  "prompt": "Summarize this quarterly report ...",
  "domain": "Analysis",
  "complexity": "High",
  "systemInstruction": "Respond only with the JSON document, no surrounding prose.",
  "responseMimeType": "application/json",
  "responseSchema": {
    "type": "object",
    "properties": {
      "summary": { "type": "string" },
      "risks": { "type": "array", "items": { "type": "string" } }
    },
    "required": ["summary", "risks"]
  }
}
```

### Response

`200 OK · application/json`

| Field | Type | Always present | Description |
|---|---|---|---|
| `completion` | string | ✅ | The model's textual answer. May be empty if the model returned no text. |
| `modelUsed` | string | ✅ | The actual model id that produced the answer (e.g. `gpt-4o`). |
| `providerUsed` | enum `AiProvider` | ✅ | The provider that produced the answer. May differ from the requested one if the fallback fired. |
| `domain` | enum `TaskDomain` | ✅ | The domain that drove the routing decision. |
| `enhancedPrompt` | string · nullable | — | The rewritten prompt — present only when `enablePromptEnhancement` was `true`. |
| `usage` | object · nullable | — | Token usage as reported by the provider. |
| `usage.inputTokens` | integer | — | Tokens in the request. |
| `usage.outputTokens` | integer | — | Tokens in the completion. |
| `usage.totalTokens` | integer | — | Sum of the two. |
| `estimatedCost` | decimal · nullable | — | USD estimate computed by `CostTracker`. `null` if the provider did not return usage. |

#### Example

```json
{
  "completion": "Konbanwa (こんばんは).",
  "modelUsed": "gemini-2.0-flash",
  "providerUsed": "Google",
  "domain": "Translation",
  "enhancedPrompt": "Translate the English greeting 'good evening' into Japanese, including the romaji and kana forms.",
  "usage": { "inputTokens": 42, "outputTokens": 15, "totalTokens": 57 },
  "estimatedCost": 0.0000102
}
```

### Errors

| Status | Reason |
|---|---|
| `400 Bad Request` | Malformed JSON or unrecognized enum values. |
| `500 Internal Server Error` | All providers failed and no fallback succeeded. The exception type indicates the cause: `InvalidOperationException` ("Primary provider X failed and no fallback provider is available") means every configured provider was exhausted. |

> AI Gateway does not currently return structured error bodies. This will change in a future release — see the [roadmap](roadmap.md).

## Enums

### `TaskDomain`

```
General | Coding | Research | Writing | Analysis | Math | Translation | Conversation
```

Serialized as strings. Case-insensitive on input.

### `ModelComplexity`

```
Low | High
```

`Low` routes to `FastModel`, `High` routes to `CapableModel`.

### `AiProvider`

```
OpenAi | Anthropic | Google | Ollama
```

`Ollama` is reserved and not yet implemented — see the [roadmap](roadmap.md).

## OpenAPI document

```http
GET /openapi/v1.json
```

Available only when `ASPNETCORE_ENVIRONMENT=Development`. Import into your favorite REST client to get autocomplete, request schemas, and example values.

## Versioning

The endpoint is mounted at `/api/v1/...`. Breaking changes will ship under `/api/v2/...`. The current `v1` surface is **pre-stable** — fields may be added but existing fields will not be removed without a deprecation notice in the [changelog](../CHANGELOG.md).
