using System.ClientModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI.Responses;
using Staj360.Application.Ai;
using Staj360.Application.Ai.Models;
using Staj360.Infrastructure.Configuration;

namespace Staj360.Infrastructure.Ai;

/// <summary>
/// OpenAI resmî .NET SDK'sı (Responses API + Structured Outputs) ile özet üretir.
/// API anahtarı yoksa devre dışıdır ve uygulamayı çökertmez. İstek/yanıt gövdeleri
/// ve hassas içerik loglanmaz.
/// </summary>
public class OpenAiProvider : IAiProvider
{
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly ResponsesClient? _client;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OpenAiProvider(IOptions<OpenAiOptions> options, ILogger<OpenAiProvider> logger)
    {
        _options = options.Value;
        _logger = logger;

        if (IsEnabled)
        {
            _client = new ResponsesClient(_options.ApiKey!);
        }
    }

    public bool IsEnabled => _options.Enabled && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string ModelName => _options.Model;

    public async Task<AiProviderResult> GenerateSummaryAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default)
    {
        if (!IsEnabled || _client is null)
            return AiProviderResult.Fail("Yapay zekâ sağlayıcısı yapılandırılmamış.");

        var createOptions = new CreateResponseOptions(_options.Model, new List<ResponseItem>
        {
            ResponseItem.CreateUserMessageItem(userPrompt)
        })
        {
            Instructions = systemPrompt,
            // Gizlilik: istek OpenAI tarafında saklanmasın.
            StoredOutputEnabled = false,
            TextOptions = new ResponseTextOptions
            {
                TextFormat = ResponseTextFormat.CreateJsonSchemaFormat(
                    "report_summary",
                    BinaryData.FromString(JsonSchema),
                    "Staj günlük raporlarının yapılandırılmış özeti",
                    jsonSchemaIsStrict: true)
            }
        };

        // Zaman aşımı + iptal.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(_options.TimeoutSeconds));

        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                ClientResult<ResponseResult> result = await _client.CreateResponseAsync(createOptions, timeoutCts.Token);
                var json = result.Value.GetOutputText();

                if (string.IsNullOrWhiteSpace(json))
                    return AiProviderResult.Fail("Yapay zekâ boş yanıt döndürdü.");

                var content = JsonSerializer.Deserialize<ReportSummaryContent>(json, JsonOptions);
                if (content is null)
                    return AiProviderResult.Fail("Yapay zekâ yanıtı beklenen biçimde değil.");

                return AiProviderResult.Ok(content);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Yapay zekâ isteği zaman aşımına uğradı (deneme {Attempt}).", attempt);
                return AiProviderResult.Fail("Yapay zekâ isteği zaman aşımına uğradı. Lütfen daha sonra tekrar deneyin.");
            }
            catch (ClientResultException ex) when (IsTransient(ex.Status) && attempt < maxAttempts)
            {
                // Geçici hata (429/5xx): kontrollü retry. Hassas içerik loglanmaz.
                _logger.LogWarning("Yapay zekâ geçici hata {Status}, yeniden deneniyor ({Attempt}/{Max}).", ex.Status, attempt, maxAttempts);
                await Task.Delay(TimeSpan.FromMilliseconds(500 * attempt), cancellationToken);
            }
            catch (ClientResultException ex)
            {
                _logger.LogError("Yapay zekâ isteği başarısız oldu. Durum: {Status}", ex.Status);
                return AiProviderResult.Fail(ex.Status == 429
                    ? "Yapay zekâ servisi şu anda yoğun. Lütfen kısa süre sonra tekrar deneyin."
                    : "Yapay zekâ özeti oluşturulamadı.");
            }
            catch (Exception)
            {
                // Ham exception kullanıcıya gösterilmez, gövde loglanmaz.
                _logger.LogError("Yapay zekâ isteğinde beklenmeyen bir hata oluştu.");
                return AiProviderResult.Fail("Yapay zekâ özeti oluşturulamadı.");
            }
        }

        return AiProviderResult.Fail("Yapay zekâ özeti oluşturulamadı.");
    }

    private static bool IsTransient(int status) => status is 429 or >= 500;

    private const string JsonSchema = """
    {
      "type": "object",
      "properties": {
        "executiveSummary": { "type": "string" },
        "completedWork": { "type": "array", "items": { "type": "string" } },
        "technologies": { "type": "array", "items": { "type": "string" } },
        "problemsAndSolutions": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "problem": { "type": "string" },
              "solution": { "type": "string" }
            },
            "required": ["problem", "solution"],
            "additionalProperties": false
          }
        },
        "risksOrBlockers": { "type": "array", "items": { "type": "string" } },
        "suggestedNextSteps": { "type": "array", "items": { "type": "string" } }
      },
      "required": ["executiveSummary", "completedWork", "technologies", "problemsAndSolutions", "risksOrBlockers", "suggestedNextSteps"],
      "additionalProperties": false
    }
    """;
}
