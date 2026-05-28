using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RaceEngineer.Core.Coaching.Ai;

public sealed class OpenAiCompatibleEngineerAiProvider : IEngineerAiProvider, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly string model;
    private readonly bool ownsClient;

    public OpenAiCompatibleEngineerAiProvider(string endpoint, string model, HttpClient? httpClient = null)
    {
        this.endpoint = string.IsNullOrWhiteSpace(endpoint)
            ? "https://api.openai.com/v1/chat/completions"
            : endpoint.Trim();
        this.model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model.Trim();
        ownsClient = httpClient is null;
        this.httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public string Name => "openai";

    public bool IsEnabled => true;

    public async Task<EngineerAiResult> GenerateAnswerAsync(EngineerAiRequest request, CancellationToken cancellationToken)
    {
        var apiKey = Environment.GetEnvironmentVariable("RACE_ENGINEER_AI_API_KEY");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return EngineerAiResult.Failed("OpenAI-compatible provider requires RACE_ENGINEER_AI_API_KEY.");
        }

        if (request.Context.Facts.Count == 0)
        {
            return EngineerAiResult.Failed("No telemetry evidence was provided.");
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(BuildPayload(request), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return EngineerAiResult.Failed($"OpenAI-compatible provider returned {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        var validated = EngineerAiResponseValidator.Validate(content ?? "", request.Context, request.MaxResponseWords);
        if (validated is null)
        {
            return EngineerAiResult.Failed("OpenAI-compatible answer failed validation.");
        }

        return EngineerAiResult.Succeeded(
            validated,
            Name,
            EngineerAiResponseValidator.BuildUncertainty(request.Context));
    }

    public void Dispose()
    {
        if (ownsClient)
        {
            httpClient.Dispose();
        }
    }

    private string BuildPayload(EngineerAiRequest request)
    {
        var facts = request.Context.Facts.Select(fact => new
        {
            topic = fact.Topic,
            summary = fact.Summary,
            detail = fact.Detail,
            confidence = fact.Confidence,
            source = fact.Source,
            lap = fact.LapNumber
        });

        var payload = new
        {
            model,
            temperature = 0.2,
            max_tokens = Math.Clamp(request.MaxResponseWords * 2, 40, 180),
            messages = new object[]
            {
                new
                {
                    role = "system",
                    content = string.Join(' ', request.Context.Guardrails)
                },
                new
                {
                    role = "user",
                    content = JsonSerializer.Serialize(new
                    {
                        question = request.Context.Question,
                        sessionMode = request.Context.SessionMode,
                        strategyConfidence = request.Context.StrategyConfidence,
                        strategyCalloutsAllowed = request.Context.StrategyCalloutsAllowed,
                        currentLap = request.Context.CurrentLap,
                        fuelLiters = request.Context.FuelLiters,
                        facts,
                        maxWords = request.MaxResponseWords
                    }, JsonOptions)
                }
            }
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
