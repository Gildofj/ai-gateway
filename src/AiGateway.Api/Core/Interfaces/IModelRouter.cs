using Microsoft.Extensions.AI;
using AiGateway.Api.Core.Models;

namespace AiGateway.Api.Core.Interfaces;

public interface IModelRouter
{
    /// <summary>
    /// Gets the most appropriate IChatClient based on the request details.
    /// </summary>
    IChatClient GetClient(ChatRequest request);
    
    /// <summary>
    /// Gets the name of the model being used by the returned client.
    /// </summary>
    string GetModelName(ChatRequest request);
}
