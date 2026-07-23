namespace Staj360.Application.Abstractions;

/// <summary>
/// Test edilebilir zaman kaynağı. Kod içinde doğrudan DateTime.Now/UtcNow kullanılmaz.
/// </summary>
public interface IClock
{
    /// <summary>Şu anki UTC zamanı.</summary>
    DateTime UtcNow { get; }
}
