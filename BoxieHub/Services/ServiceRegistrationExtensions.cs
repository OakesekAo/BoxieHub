using BoxieHub.Services.BoxieCloud;
using BoxieHub.Services.Sync;

namespace BoxieHub.Services;

/// <summary>
/// Extension methods for registering BoxieCloud services in the DI container.
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Registers BoxieCloud services with the DI container.
    /// Includes authentication, Tonie Cloud API client, and S3 storage.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBoxieCloudServices(this IServiceCollection services)
    {
        // Add in-memory cache for token caching
        services.AddMemoryCache();
        
        // Register credential encryption service (Singleton - stateless, uses config key)
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        
        // Register auth service with HttpClient (Scoped for per-request isolation)
        services.AddHttpClient<IBoxieAuthService, BoxieAuthService>();
        services.AddScoped<IBoxieAuthService, BoxieAuthService>();
        
        // Register S3 storage service with HttpClient (Scoped)
        services.AddHttpClient<IS3StorageService, S3StorageService>();
        services.AddScoped<IS3StorageService, S3StorageService>();
        
        // Register BoxieCloud client with HttpClient (Scoped)
        services.AddHttpClient<IBoxieCloudClient, BoxieCloudClient>();
        services.AddScoped<IBoxieCloudClient, BoxieCloudClient>();
        
        // Register sync job service
        services.AddScoped<ISyncJobService, SyncJobService>();
        
        return services;
    }
}


