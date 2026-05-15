using System.ClientModel;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AiGateway.Api.Infrastructure.AiProviders;

public sealed class AnthropicClientFactory : IProviderClientFactory
{
    public AiProvider Provider => AiProvider.Anthropic;

    public IChatClient Create(ProviderDescriptor descriptor, ModelComplexity complexity)
    {
        var modelId = complexity == ModelComplexity.High ? descriptor.CapableModel : descriptor.FastModel;

        var credential = new ApiKeyCredential(descriptor.ApiKey);
        var options = descriptor.Endpoint is not null
            ? new OpenAIClientOptions { Endpoint = descriptor.Endpoint }
            : new OpenAIClientOptions();

        return new ChatClient(modelId, credential, options).AsIChatClient();
    }
}
