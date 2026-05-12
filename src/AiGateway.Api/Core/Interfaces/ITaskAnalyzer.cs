using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Core.Interfaces;

public interface ITaskAnalyzer
{
    Task<TaskAnalysis> AnalyzeAsync(string prompt, CancellationToken cancellationToken = default);
}
