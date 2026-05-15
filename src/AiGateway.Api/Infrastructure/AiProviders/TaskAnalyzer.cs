using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace AiGateway.Api.Infrastructure.AiProviders;

public class TaskAnalyzer : ITaskAnalyzer
{
    private readonly IProviderRegistry _registry;
    private readonly ILogger<TaskAnalyzer> _logger;

    public TaskAnalyzer(IProviderRegistry registry, ILogger<TaskAnalyzer> logger)
    {
        _registry = registry;
        _logger = logger;
        if (registry.GetAvailable().Count == 0)
            throw new InvalidOperationException("No AI providers are configured. Set at least one API key in appsettings or environment variables.");
    }

    public async Task<TaskAnalysis> AnalyzeAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var heuristic = TryHeuristic(prompt);
        if (heuristic is not null)
            return heuristic;

        return await AnalyzeWithAiAsync(prompt, cancellationToken);
    }

    private static TaskAnalysis? TryHeuristic(string prompt)
    {
        if (prompt.Length < 25)
            return new TaskAnalysis(TaskDomain.Conversation, ModelComplexity.Low);

        var lower = prompt.ToLowerInvariant();

        if (ContainsAny(lower, "oi", "olá", "hello", "hi", "hey", "tchau", "bye", "obrigado", "thanks", "tudo bem"))
            return new TaskAnalysis(TaskDomain.Conversation, ModelComplexity.Low);

        if (ContainsAny(lower, "traduza", "translate", "tradução", "translation"))
            return new TaskAnalysis(TaskDomain.Translation, ModelComplexity.Low);

        if (ContainsAny(lower, "calcule", "calculate", "resolva", "solve", "integral", "derivada", "derivative", "equação", "equation", "matemática", "mathematics"))
            return new TaskAnalysis(TaskDomain.Math, ModelComplexity.High);

        return null;
    }

    private async Task<TaskAnalysis> AnalyzeWithAiAsync(string prompt, CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt =
                "Classify the user prompt. Return ONLY valid JSON with two fields:\n" +
                "  \"domain\": one of General|Coding|Research|Writing|Analysis|Math|Translation|Conversation\n" +
                "  \"complexity\": one of Low|High\n\n" +
                "Coding/Debugging/Refactoring → domain=Coding, complexity=High\n" +
                "Greetings/casual chat → domain=Conversation, complexity=Low\n" +
                "Translation → domain=Translation, complexity=Low\n" +
                "Search/news/current events → domain=Research, complexity=Low\n" +
                "Document/data interpretation → domain=Analysis, complexity=High\n" +
                "Essays/emails/creative text → domain=Writing, complexity=Low or High based on length\n" +
                "Math/statistics → domain=Math, complexity=High\n" +
                "Return ONLY the JSON object, no markdown, no explanation.";

            var preferred = _registry.GetAvailable()[0].Provider;
            var response = await _registry.ExecuteAsync(
                preferred,
                ModelComplexity.Low,
                async ctx =>
                {
                    var options = new ChatOptions { ModelId = ctx.ModelName };
                    return await ctx.Client.GetResponseAsync(
                    [
                        new ChatMessage(ChatRole.System, systemPrompt),
                        new ChatMessage(ChatRole.User, $"Classify: {prompt}")
                    ], options, cancellationToken);
                },
                allowFallback: true,
                cancellationToken: cancellationToken);

            return ParseAiResponse(response.Text);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AI task analysis failed. Falling back to General/Low.");
            return new TaskAnalysis(TaskDomain.General, ModelComplexity.Low);
        }
    }

    private static TaskAnalysis ParseAiResponse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new TaskAnalysis(TaskDomain.General, ModelComplexity.Low);

        try
        {
            var cleaned = json.Trim().TrimStart('`').TrimEnd('`');
            if (cleaned.StartsWith("json", StringComparison.OrdinalIgnoreCase))
                cleaned = cleaned[4..].Trim();

            using var doc = JsonDocument.Parse(cleaned);
            var root = doc.RootElement;

            var domain = root.TryGetProperty("domain", out var d) && Enum.TryParse<TaskDomain>(d.GetString(), true, out var td)
                ? td : TaskDomain.General;

            var complexity = root.TryGetProperty("complexity", out var c) && Enum.TryParse<ModelComplexity>(c.GetString(), true, out var mc)
                ? mc : ModelComplexity.Low;

            return new TaskAnalysis(domain, complexity);
        }
        catch
        {
            return new TaskAnalysis(TaskDomain.General, ModelComplexity.Low);
        }
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
}
