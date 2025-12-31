using BoxieHub.Services.PythonAdapter;
using BoxieHub.Services.Sync;
using BoxieHub.TonieCloud.Services;

namespace BoxieHub.Services;

/// <summary>
/// Extension methods for registering Python adapter and sync services in the DI container.
/// </summary>
public static class ServiceRegistrationExtensions
{
    /// <summary>
    /// Registers Python adapter and sync services with the DI container.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="pythonAdapterBaseUrl">Base URL of the Python adapter service (e.g., http://localhost:8000).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPythonAdapterServices(
        this IServiceCollection services,
        string pythonAdapterBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(pythonAdapterBaseUrl))
            throw new ArgumentException("Python adapter base URL cannot be empty", nameof(pythonAdapterBaseUrl));

        // Register HttpClient for Python adapter with base address
        services.AddHttpClient<IPythonAdapterClient, PythonAdapterClient>(client =>
        {
            client.BaseAddress = new Uri(pythonAdapterBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register sync job service
        services.AddScoped<ISyncJobService, SyncJobService>();

        return services;
    }
    
    /// <summary>
    /// Registers C# Tonie Cloud services with the DI container.
    /// This is the new implementation that replaces the Python adapter.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTonieCloudServices(this IServiceCollection services)
    {
        // Add in-memory cache for token caching
        services.AddMemoryCache();
        
        // Register auth service with HttpClient
        services.AddHttpClient<ITonieAuthService, TonieAuthService>();
        
        // Register Tonie Cloud client with HttpClient
        services.AddHttpClient<ITonieCloudClient, TonieCloudClient>();
        
        // Register sync job service
        services.AddScoped<ISyncJobService, SyncJobService>();
        
        return services;
    }
}

