using System;
using System.Threading;
using System.Threading.Tasks;

namespace BoxieHub.Tests.Integration;

/// <summary>
/// Helper class for retrying operations that may fail due to transient issues
/// </summary>
public static class RetryHelper
{
    /// <summary>
    /// Retry an async operation with exponential backoff
    /// </summary>
    public static async Task<T> RetryAsync<T>(
        Func<Task<T>> operation,
        int maxAttempts = 3,
        int delayMilliseconds = 1000,
        string operationName = "Operation")
    {
        Exception? lastException = null;
        
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                lastException = ex;
                var delay = delayMilliseconds * attempt; // Exponential backoff
                
                Console.WriteLine($"?? {operationName} failed (attempt {attempt}/{maxAttempts}): {ex.Message}");
                Console.WriteLine($"   Retrying in {delay}ms...");
                
                await Task.Delay(delay);
            }
        }
        
        // If we get here, all attempts failed
        throw new InvalidOperationException(
            $"{operationName} failed after {maxAttempts} attempts. Last error: {lastException?.Message}",
            lastException);
    }
}
