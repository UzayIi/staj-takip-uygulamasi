namespace Staj360.Application.Common;

/// <summary>
/// Uygulama servislerinin standart sonuç modeli. Beklenen iş kuralı hatalarında
/// exception fırlatmak yerine bu yapı ile Türkçe ve anlaşılır mesaj döndürülür.
/// </summary>
public class ServiceResult
{
    public bool Success { get; protected set; }
    public string? ErrorCode { get; protected set; }
    public string? ErrorMessage { get; protected set; }
    public IReadOnlyList<string> ValidationErrors { get; protected set; } = Array.Empty<string>();

    public static ServiceResult Ok() => new() { Success = true };

    public static ServiceResult Fail(string errorMessage, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };

    public static ServiceResult Invalid(IEnumerable<string> validationErrors) => new()
    {
        Success = false,
        ErrorCode = "VALIDATION",
        ErrorMessage = "Doğrulama hatası oluştu.",
        ValidationErrors = validationErrors.ToList()
    };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };

    public static new ServiceResult<T> Fail(string errorMessage, string? errorCode = null) => new()
    {
        Success = false,
        ErrorMessage = errorMessage,
        ErrorCode = errorCode
    };

    public static new ServiceResult<T> Invalid(IEnumerable<string> validationErrors) => new()
    {
        Success = false,
        ErrorCode = "VALIDATION",
        ErrorMessage = "Doğrulama hatası oluştu.",
        ValidationErrors = validationErrors.ToList()
    };
}
