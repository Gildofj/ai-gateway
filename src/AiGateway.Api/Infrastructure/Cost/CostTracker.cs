using AiGateway.Api.Core.Interfaces;

namespace AiGateway.Api.Infrastructure.Cost;

public class CostTracker : ICostTracker
{
    public decimal EstimateCost(string model, long inputTokens, long outputTokens)
    {
        var pricing = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-5.4-mini") => (input: 0.75m, output: 4.50m),
            var m when m.Contains("gpt-5.5") => (input: 5.00m, output: 30.00m),
            var m when m.Contains("gemini-3.0-flash") => (input: 0.50m, output: 3.00m),
            var m when m.Contains("gemini-3.1-pro") => (input: 2.00m, output: 12.00m),
            var m when m.Contains("claude-4-haiku") => (input: 1.00m, output: 5.00m),
            var m when m.Contains("claude-4-sonnet") => (input: 3.00m, output: 15.00m),
            _ => (input: 1.00m, output: 3.00m)
        };

        return inputTokens / 1_000_000m * pricing.input
             + outputTokens / 1_000_000m * pricing.output;
    }
}
