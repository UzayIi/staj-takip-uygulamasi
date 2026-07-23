using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Logging;

namespace Staj360.Infrastructure.Services;

public class DummyEmailSender : IEmailSender
{
    private readonly ILogger<DummyEmailSender> _logger;

    public DummyEmailSender(ILogger<DummyEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("================================================");
        _logger.LogInformation($"DUMMY EMAIL GÖNDERİLDİ:");
        _logger.LogInformation($"Kime: {email}");
        _logger.LogInformation($"Konu: {subject}");
        _logger.LogInformation($"Mesaj: {htmlMessage}");
        _logger.LogInformation("================================================");

        return Task.CompletedTask;
    }
}
