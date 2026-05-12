using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Core.Interfaces;

public interface IModelRouter
{
    IChatClient GetClient(RoutingDecision decision);
    string GetModelName(RoutingDecision decision);
}
