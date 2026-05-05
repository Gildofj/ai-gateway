using AiGateway.Api.Core.Interfaces;
using AiGateway.Api.Core.Models;
using Microsoft.Extensions.AI;

namespace AiGateway.Api.Features.Routing;

public class ModelRouter : IModelRouter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public ModelRouter(IServiceProvider serviceProvider, IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public IChatClient GetClient(ChatRequest request)
    {
        var provider = ResolveProvider(request);
        var key = $"{provider.ToString().ToLower()}-{(request.Complexity == ModelComplexity.High ? "capable" : "fast")}";
        
        return _serviceProvider.GetRequiredKeyedService<IChatClient>(key);
    }

    public string GetModelName(ChatRequest request)
    {
        var provider = ResolveProvider(request);
        var configKey = $"AI:{provider}:{(request.Complexity == ModelComplexity.High ? "Capable" : "Fast")}ModelName";
        
        return _configuration[configKey] ?? GetDefaultModelName(provider, request.Complexity);
    }

    private AiProvider ResolveProvider(ChatRequest request)
    {
        if (request.Provider.HasValue)
            return request.Provider.Value;

        if (!request.AutoProviderSelection)
            return AiProvider.OpenAi; // Default

        // Intelligent Auto-Selection Logic
        // This is where you'd implement logic based on cost, latency, or task type.
        return request.Complexity switch
        {
            ModelComplexity.Low => AiProvider.Google, // Gemini 1.5 Flash is currently very cheap/fast
            ModelComplexity.High => AiProvider.OpenAi, // GPT-4o is very reliable for complex tasks
            _ => AiProvider.OpenAi
        };
    }

    private string GetDefaultModelName(AiProvider provider, ModelComplexity complexity)
    {
        return provider switch
        {
            AiProvider.OpenAi => complexity == ModelComplexity.High ? "gpt-4o" : "gpt-4o-mini",
            AiProvider.Google => complexity == ModelComplexity.High ? "gemini-1.5-pro" : "gemini-1.5-flash",
            AiProvider.Anthropic => complexity == ModelComplexity.High ? "claude-3-5-sonnet-20240620" : "claude-3-haiku-20240307",
            _ => "gpt-4o-mini"
        };
    }
}
