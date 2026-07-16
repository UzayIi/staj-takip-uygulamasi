namespace Staj360.Application.Abstractions;

public record FileSaveRequest(string FileName, string ContentType, long Length, Stream Content);

/// <summary>
/// Güvenli yerel dosya servis arayüzü. İzin belgeleri gibi yüklemeler için kullanılır.
/// Uzantı, MIME ve boyut doğrulaması implementasyonda yapılır; çalıştırılabilir dosyalar reddedilir.
/// </summary>
public interface IFileStorageService
{
    Task<ServiceFileResult> SaveAsync(FileSaveRequest request, string subFolder, CancellationToken cancellationToken = default);
}

public record ServiceFileResult(bool Success, string? RelativePath, string? ErrorMessage);
