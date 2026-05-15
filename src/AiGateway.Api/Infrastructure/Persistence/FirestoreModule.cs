using Google.Cloud.Firestore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiGateway.Api.Infrastructure.Persistence;

public static class FirestoreModule
{
    public static IServiceCollection AddFirestore(this IServiceCollection services, IConfiguration configuration)
    {
        var projectId = configuration["GCP_PROJECT_ID"];

        if (string.IsNullOrEmpty(projectId))
        {
            return services;
        }

        services.AddSingleton(sp => 
        {
            var emulatorHost = Environment.GetEnvironmentVariable("FIRESTORE_EMULATOR_HOST");

            var builder = new FirestoreDbBuilder
            {
                ProjectId = projectId,
                EmulatorDetection = Google.Api.Gax.EmulatorDetection.EmulatorOrProduction
            };

            if (!string.IsNullOrEmpty(emulatorHost))
            {
                builder.ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure;
            }

            return builder.Build();
        });

        return services;
    }
}
