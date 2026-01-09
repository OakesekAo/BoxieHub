using Amazon.S3;
using Amazon.Runtime;
using BoxieHub.Services.BoxieCloud;
using BoxieHub.Services.Storage;
using BoxieHub.Services.Sync;
using BoxieHub.Services.Import;

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
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBoxieCloudServices(this IServiceCollection services, IConfiguration configuration)
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
        
        // Register Tonie service (high-level business logic)
        services.AddScoped<ITonieService, TonieService>();
        services.AddScoped<IMediaLibraryService, MediaLibraryService>();
        services.AddScoped<IStoragePreferenceService, StoragePreferenceService>();
        
        // Register file storage services
        services.AddFileStorageServices(configuration);
        
        // Register YouTube import services
        services.AddImportServices();

        return services;
    }
    
    /// <summary>
    /// Registers file storage services (S3, Database fallback)
    /// </summary>
    private static IServiceCollection AddFileStorageServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configure AWS S3 Client (works with MinIO and other S3-compatible services)
        var s3Config = configuration.GetSection("S3Storage");
        var serviceUrl = s3Config["ServiceUrl"];
        var accessKey = s3Config["AccessKey"];
        var secretKey = s3Config["SecretKey"];
        var region = s3Config["Region"] ?? "us-east-1";
        var forcePathStyle = s3Config.GetValue<bool>("ForcePathStyle", false);
        
        if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
        {
            // Configure S3 client with credentials
            services.AddSingleton<IAmazonS3>(sp =>
            {
                var s3ClientConfig = new AmazonS3Config
                {
                    RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
                    ForcePathStyle = forcePathStyle
                };
                
                // Set custom service URL (for MinIO or Railway S3)
                if (!string.IsNullOrEmpty(serviceUrl))
                {
                    s3ClientConfig.ServiceURL = serviceUrl;
                }
                
                var credentials = new BasicAWSCredentials(accessKey, secretKey);
                return new AmazonS3Client(credentials, s3ClientConfig);
            });
            
            // Register S3 file storage service
            services.AddScoped<S3FileStorageService>();
        }
        
        // Register database file storage service (fallback)
        services.AddScoped<DatabaseFileStorageService>();
        
        // Register file storage factory
        services.AddScoped<IFileStorageService>(sp =>
        {
            // Use S3 if configured, otherwise fall back to database
            if (!string.IsNullOrEmpty(accessKey) && !string.IsNullOrEmpty(secretKey))
            {
                return sp.GetRequiredService<S3FileStorageService>();
            }
            
            return sp.GetRequiredService<DatabaseFileStorageService>();
        });
        
        
        return services;
    }
    
    /// <summary>
    /// Registers import services (YouTube, background processor)
    /// </summary>
    private static IServiceCollection AddImportServices(this IServiceCollection services)
    {
        // Register YouTube import service
        services.AddScoped<IYouTubeImportService, YouTubeImportService>();
        
        // Register import job service
        services.AddScoped<IImportJobService, ImportJobService>();
        
        // Register background job processor as singleton (runs once per app lifetime)
        services.AddSingleton<ImportJobProcessor>();
        services.AddHostedService(sp => sp.GetRequiredService<ImportJobProcessor>());
        
        return services;
    }
}








