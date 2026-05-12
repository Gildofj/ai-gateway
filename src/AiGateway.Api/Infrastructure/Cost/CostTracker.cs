using AiGateway.Api.Core.Interfaces;

namespace AiGateway.Api.Infrastructure.Cost;

public class CostTracker : ICostTracker
{
    public decimal EstimateCost(string model, long inputTokens, long outputTokens)
    {
        var pricing = model.ToLowerInvariant() switch
        {
            var m when m.Contains("gpt-4o-mini") => (input: 0.15m, output: 0.60m),
            var m when m.Contains("gpt-4o") => (input: 5.00m, output: 15.00m),
            var m when m.Contains("gemini-2.0-flash") => (input: 0.10m, output: 0.40m),
            var m when m.Contains("gemini-2.0-pro") => (input: 3.50m, output: 10.50m),
            var m when m.Contains("gemini-1.5-flash") => (input: 0.075m, output: 0.30m),
            var m when m.Contains("gemini-1.5-pro") => (input: 3.50m, output: 10.50m),
            var m when m.Contains("haiku") => (input: 0.25m, output: 1.25m),
            var m when m.Contains("sonnet") => (input: 3.00m, output: 15.00m),
            _ => (input: 1.00m, output: 3.00m)
        };

        return inputTokens / 1_000_000m * pricing.input
             + outputTokens / 1_000_000m * pricing.output;
    }
}
