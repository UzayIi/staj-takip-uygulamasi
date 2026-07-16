using Microsoft.Extensions.Logging;
using Staj360.Application.Abstractions;

namespace Staj360.Infrastructure.Services;

/// <summary>
/// Güvenli yerel dosya depolama. Uzantı, MIME ve boyut doğrulanır; çalıştırılabilir
/// dosyalar reddedilir. Üretimde bulut depolama ile değiştirilebilir.
/// </summary>
public class LocalFileStorageService : IFileStorageService
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".docx"
    };

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
    };

    // Kesinlikle reddedilen çalıştırılabilir/riskli uzantılar.
    private static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".dll", ".bat", ".cmd", ".com", ".msi", ".ps1", ".sh", ".js", ".vbs", ".scr", ".jar"
    };

    private readonly string _rootPath;
    private readonly ILogger<LocalFileStorageService> _logger;

    public LocalFileStorageService(string rootPath, ILogger<LocalFileStorageService> logger)
    {
        _rootPath = rootPath;
        _logger = logger;
    }

    public async Task<ServiceFileResult> SaveAsync(FileSaveRequest request, string subFolder, CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(request.FileName);

        if (string.IsNullOrWhiteSpace(extension) || BlockedExtensions.Contains(extension))
            return new ServiceFileResult(false, null, "Bu dosya türü güvenlik nedeniyle kabul edilmiyor.");

        if (!AllowedExtensions.Contains(extension))
            return new ServiceFileResult(false, null, "Yalnızca PDF, resim ve Word belgeleri yüklenebilir.");

        if (!AllowedContentTypes.Contains(request.ContentType))
            return new ServiceFileResult(false, null, "Dosya içerik türü doğrulanamadı.");

        if (request.Length <= 0 || request.Length > MaxFileSizeBytes)
            return new ServiceFileResult(false, null, "Dosya boyutu 5 MB sınırını aşıyor veya boş.");

        // Dizin geçişini engellemek için güvenli, rastgele dosya adı üret.
        var safeName = $"{Guid.NewGuid():N}{extension}";
        var targetDir = Path.Combine(_rootPath, SanitizeSubFolder(subFolder));
        Directory.CreateDirectory(targetDir);
        var fullPath = Path.Combine(targetDir, safeName);

        await using (var fs = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write))
        {
            await request.Content.CopyToAsync(fs, cancellationToken);
        }

        var relative = Path.Combine(SanitizeSubFolder(subFolder), safeName).Replace('\\', '/');
        _logger.LogInformation("Dosya kaydedildi: {RelativePath}", relative);
        return new ServiceFileResult(true, relative, null);
    }

    private static string SanitizeSubFolder(string subFolder)
    {
        var cleaned = new string(subFolder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_').ToArray());
        return string.IsNullOrWhiteSpace(cleaned) ? "misc" : cleaned;
    }
}
