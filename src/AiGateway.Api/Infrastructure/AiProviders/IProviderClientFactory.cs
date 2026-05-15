using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public interface IProviderClientFactory
{
    AiProvider Provider { get; }

    IChatClient Create(ProviderDescriptor descriptor, ModelComplexity complexity);
}
