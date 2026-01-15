namespace BoxieHub.Services;

/// <summary>
/// Service for displaying toast notifications in Blazor Server
/// Uses Bootstrap toast component for styling
/// </summary>
public interface IToastService
{
    event Action<ToastMessage>? OnShow;
    void ShowSuccess(string message, string? title = null);
    void ShowError(string message, string? title = null);
    void ShowInfo(string message, string? title = null);
    void ShowWarning(string message, string? title = null);
}

public class ToastService : IToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, string? title = null)
        => OnShow?.Invoke(new ToastMessage(title ?? "Success", message, ToastType.Success));

    public void ShowError(string message, string? title = null)
        => OnShow?.Invoke(new ToastMessage(title ?? "Error", message, ToastType.Error));

    public void ShowInfo(string message, string? title = null)
        => OnShow?.Invoke(new ToastMessage(title ?? "Info", message, ToastType.Info));

    public void ShowWarning(string message, string? title = null)
        => OnShow?.Invoke(new ToastMessage(title ?? "Warning", message, ToastType.Warning));
}

public record ToastMessage(string Title, string Message, ToastType Type)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.Now;
}

public enum ToastType
{
    Success,
    Error,
    Info,
    Warning
}
