using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

internal sealed class GeminiChatClient : IChatClient
{
    private static readonly Uri DefaultBase = new("https://generativelanguage.googleapis.com/v1beta/");

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _modelId;
    private readonly ChatClientMetadata _metadata;

    public GeminiChatClient(HttpClient http, string apiKey, string modelId)
    {
        _http = http;
        _apiKey = apiKey;
        _modelId = modelId;
        if (_http.BaseAddress is null) _http.BaseAddress = DefaultBase;
        _metadata = new ChatClientMetadata("google", _http.BaseAddress, modelId);
    }

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var (contents, systemInstruction) = TranslateMessages(messages);
        var modelId = options?.ModelId ?? _modelId;

        var payload = new GeminiRequest
        {
            Contents = contents,
            SystemInstruction = systemInstruction,
            GenerationConfig = BuildGenerationConfig(options),
            Tools = BuildTools(options)
        };

        var url = $"models/{modelId}:generateContent?key={Uri.EscapeDataString(_apiKey)}";

        using var httpResponse = await _http.PostAsJsonAsync(url, payload, JsonOptions, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await httpResponse.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Gemini request failed with status {(int)httpResponse.StatusCode}: {body}");
        }

        var response = await httpResponse.Content.ReadFromJsonAsync<GeminiResponse>(JsonOptions, cancellationToken)
            ?? throw new InvalidOperationException("Gemini returned an empty response.");

        var candidate = response.Candidates?.FirstOrDefault();
        var text = candidate?.Content?.Parts?
            .Select(p => p.Text)
            .Where(t => !string.IsNullOrEmpty(t))
            .DefaultIfEmpty(string.Empty)
            .Aggregate((a, b) => a + b) ?? string.Empty;

        var chatResponse = new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = modelId,
            FinishReason = MapFinishReason(candidate?.FinishReason)
        };

        if (response.UsageMetadata is { } u)
        {
            chatResponse.Usage = new UsageDetails
            {
                InputTokenCount = u.PromptTokenCount,
                OutputTokenCount = u.CandidatesTokenCount,
                TotalTokenCount = u.TotalTokenCount
            };
        }

        return chatResponse;
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Streaming is not supported by the Gemini adapter yet.");

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        if (serviceType == typeof(ChatClientMetadata)) return _metadata;
        if (serviceType?.IsInstanceOfType(this) == true) return this;
        return null;
    }

    public void Dispose()
    {
        // HttpClient is owned by IHttpClientFactory — do not dispose it here.
    }

    private static (List<GeminiContent> Contents, GeminiContent? System) TranslateMessages(IEnumerable<ChatMessage> messages)
    {
        var contents = new List<GeminiContent>();
        var systemTexts = new List<string>();

        foreach (var msg in messages)
        {
            var text = msg.Text;
            if (string.IsNullOrEmpty(text)) continue;

            if (msg.Role == ChatRole.System)
            {
                systemTexts.Add(text);
                continue;
            }

            var role = msg.Role == ChatRole.Assistant ? "model" : "user";
            contents.Add(new GeminiContent
            {
                Role = role,
                Parts = [new GeminiPart { Text = text }]
            });
        }

        GeminiContent? system = systemTexts.Count > 0
            ? new GeminiContent
            {
                Role = "system",
                Parts = [new GeminiPart { Text = string.Join("\n\n", systemTexts) }]
            }
            : null;

        return (contents, system);
    }

    private static GeminiGenerationConfig? BuildGenerationConfig(ChatOptions? options)
    {
        if (options is null) return null;

        var hasResponseFormat = options.ResponseFormat is ChatResponseFormatJson;
        if (options.Temperature is null
            && options.MaxOutputTokens is null
            && options.TopP is null
            && (options.StopSequences is null || options.StopSequences.Count == 0)
            && !hasResponseFormat)
        {
            return null;
        }

        var config = new GeminiGenerationConfig
        {
            Temperature = options.Temperature,
            MaxOutputTokens = options.MaxOutputTokens,
            TopP = options.TopP,
            StopSequences = options.StopSequences?.Count > 0 ? options.StopSequences.ToArray() : null
        };

        if (options.ResponseFormat is ChatResponseFormatJson json)
        {
            config = config with
            {
                ResponseMimeType = "application/json",
                ResponseSchema = json.Schema
            };
        }

        return config;
    }

    private static IList<GeminiTool>? BuildTools(ChatOptions? options)
    {
        if (options?.Tools is null || options.Tools.Count == 0) return null;

        var declarations = new List<GeminiFunctionDeclaration>();
        foreach (var tool in options.Tools)
        {
            if (tool is not AIFunction function) continue;

            declarations.Add(new GeminiFunctionDeclaration
            {
                Name = function.Name,
                Description = function.Description,
                Parameters = function.JsonSchema
            });
        }

        return declarations.Count == 0 ? null : [new GeminiTool { FunctionDeclarations = declarations }];
    }

    private static ChatFinishReason? MapFinishReason(string? reason) => reason switch
    {
        "STOP" => ChatFinishReason.Stop,
        "MAX_TOKENS" => ChatFinishReason.Length,
        "SAFETY" or "RECITATION" or "BLOCKLIST" or "PROHIBITED_CONTENT" or "SPII" => ChatFinishReason.ContentFilter,
        "MALFORMED_FUNCTION_CALL" or "FINISH_REASON_UNSPECIFIED" or null => null,
        _ => null
    };

    private sealed record GeminiRequest
    {
        public required IList<GeminiContent> Contents { get; init; }
        public GeminiContent? SystemInstruction { get; init; }
        public GeminiGenerationConfig? GenerationConfig { get; init; }
        public IList<GeminiTool>? Tools { get; init; }
    }

    private sealed record GeminiContent
    {
        public string? Role { get; init; }
        public required IList<GeminiPart> Parts { get; init; }
    }

    private sealed record GeminiPart
    {
        public string? Text { get; init; }
    }

    private sealed record GeminiGenerationConfig
    {
        public float? Temperature { get; init; }
        public int? MaxOutputTokens { get; init; }
        public float? TopP { get; init; }
        public IList<string>? StopSequences { get; init; }
        public string? ResponseMimeType { get; init; }
        public JsonElement? ResponseSchema { get; init; }
    }

    private sealed record GeminiTool
    {
        public IList<GeminiFunctionDeclaration>? FunctionDeclarations { get; init; }
    }

    private sealed record GeminiFunctionDeclaration
    {
        public required string Name { get; init; }
        public string? Description { get; init; }
        public JsonElement? Parameters { get; init; }
    }

    private sealed record GeminiResponse
    {
        public IList<GeminiCandidate>? Candidates { get; init; }
        public GeminiUsageMetadata? UsageMetadata { get; init; }
    }

    private sealed record GeminiCandidate
    {
        public GeminiContent? Content { get; init; }
        public string? FinishReason { get; init; }
    }

    private sealed record GeminiUsageMetadata
    {
        public int? PromptTokenCount { get; init; }
        public int? CandidatesTokenCount { get; init; }
        public int? TotalTokenCount { get; init; }
    }
}
