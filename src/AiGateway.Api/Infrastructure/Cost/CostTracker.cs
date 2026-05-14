using AiGateway.Api.Core.Interfaces;

namespace AiGateway.Api.Infrastructure.Cost;

public class CostTracker : ICostTracker
{
    public decimal EstimateCost(string model, long inputTokens, long outputTokens)
    {
        // GA Prices as of May 2026 (USD per 1M tokens)
        var pricing = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-5.4-mini") => (input: 0.10m, output: 0.40m),
            var m when m.Contains("gpt-5.5-thinking") => (input: 2.00m, output: 10.00m),
            var m when m.Contains("gemini-3.1-flash") => (input: 0.05m, output: 0.20m),
            var m when m.Contains("gemini-3.1-pro") => (input: 1.00m, output: 4.00m),
            var m when m.Contains("haiku-4.5") => (input: 0.15m, output: 0.60m),
            var m when m.Contains("opus-4.7") => (input: 2.50m, output: 15.00m),
            _ => (input: 1.00m, output: 3.00m)
        };

        return inputTokens / 1_000_000m * pricing.input
             + outputTokens / 1_000_000m * pricing.output;
    }
}
