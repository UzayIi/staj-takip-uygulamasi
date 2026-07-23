namespace Staj360.Application.Services.Exports;

public record InternExcelExportResult(byte[] Content, string FileName, string ContentType);

/// <summary>Admin stajyer detayının .xlsx dışa aktarımı.</summary>
public interface IInternExcelExportService
{
    /// <returns>Stajyer yoksa null.</returns>
    Task<InternExcelExportResult?> ExportAsync(Guid internProfileId, CancellationToken cancellationToken = default);
}
