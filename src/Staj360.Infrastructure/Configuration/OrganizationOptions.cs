namespace Staj360.Infrastructure.Configuration;

public class OrganizationOptions
{
    public const string SectionName = "Organization";
    public string Name { get; set; } = "Kurum Adı";
    /// <summary>Kullanıcıya görünen ürün/marka adı (ör. StajAmed). Teknik assembly adından bağımsızdır.</summary>
    public string BrandName { get; set; } = "StajAmed";
    public string TimeZone { get; set; } = "Europe/Istanbul";
}

public class AttendanceOptions
{
    public const string SectionName = "Attendance";
    public int ReportDurationDifferenceWarningMinutes { get; set; } = 60;
}

public class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public bool Enabled { get; set; }
    public string Model { get; set; } = "gpt-5.6-luna";
    public string PromptVersion { get; set; } = "v1";
    public int TimeoutSeconds { get; set; } = 60;
    public int MaxInputCharacters { get; set; } = 30000;

    // API anahtarı YALNIZCA OPENAI_API_KEY env veya User Secrets üzerinden okunur.
    // appsettings.json / Git / log / veritabanına yazılmaz.
    public string? ApiKey { get; set; }
}
