namespace AiGateway.Api.Core.Models;

public class ProviderNotConfiguredException : Exception
{
    public AiProvider Provider { get; }

    public ProviderNotConfiguredException(AiProvider provider)
        : base($"Provider {provider} is not configured on this gateway.")
    {
        Provider = provider;
    }
}
