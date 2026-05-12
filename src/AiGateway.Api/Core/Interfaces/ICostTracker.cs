namespace AiGateway.Api.Core.Interfaces;

public interface ICostTracker
{
    decimal EstimateCost(string model, long inputTokens, long outputTokens);
}
