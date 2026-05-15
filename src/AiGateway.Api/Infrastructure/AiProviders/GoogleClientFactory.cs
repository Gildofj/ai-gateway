using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Infrastructure.AiProviders;

public sealed class GoogleClientFactory : IProviderClientFactory
{
    public const string HttpClientName = "gemini";

    private readonly IHttpClientFactory _httpClientFactory;

    public GoogleClientFactory(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public AiProvider Provider => AiProvider.Google;

    public IChatClient Create(ProviderDescriptor descriptor, ModelComplexity complexity)
    {
        var modelId = complexity == ModelComplexity.High ? descriptor.CapableModel : descriptor.FastModel;
        var http = _httpClientFactory.CreateClient(HttpClientName);
        return new GeminiChatClient(http, descriptor.ApiKey, modelId);
    }
}
