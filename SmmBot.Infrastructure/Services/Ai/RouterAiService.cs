using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SmmBot.Core.Interfaces.Ai;
using SmmBot.Core.Interfaces.Settings.Models;

namespace SmmBot.Infrastructure.Services.Ai;

public class RouterAiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RouterAiService> _logger;
    private readonly BotConfiguration _config;

    public RouterAiService(HttpClient httpClient, IOptions<BotConfiguration> config, ILogger<RouterAiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config.Value;

        _httpClient.BaseAddress = new Uri(_config.AiUri);
        _httpClient.Timeout = new TimeSpan(0, 10, 0);
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _config.AiToken);
    }

    public async Task<string> GenerateContentPlanAsync(string systemPrompt, string previousPlans, string statistics, DateTimeOffset targetWeekStart, CancellationToken cancellationToken = default)
    {
        var targetWeekEnd = targetWeekStart.AddDays(7).AddTicks(-1);
        var prompt = $"Create a content plan for the week from {targetWeekStart:yyyy-MM-dd} to {targetWeekEnd:yyyy-MM-dd} based on this system prompt: {systemPrompt}\n";
        prompt += $"IMPORTANT DATE INFO: All generated 'scheduledTime' fields MUST be within the dates {targetWeekStart:yyyy-MM-dd} and {targetWeekEnd:yyyy-MM-dd}.\n";
        if (!string.IsNullOrEmpty(previousPlans)) prompt += $"Previous plans to avoid repetition:\n{previousPlans}\n";
        if (!string.IsNullOrEmpty(statistics)) prompt += $"Statistics to consider:\n{statistics}\n";
        prompt += "Format the output strictly as a JSON array of objects, with each object containing 'text' (string), 'scheduledTime' (string in ISO 8601 format), and 'mediaRecommendation' (string) properties. The 'mediaRecommendation' field should contain instructions on what kind of media (photo/video) should accompany this post, keeping it separate from the post 'text'. Do not include any markdown formatting, backticks, or text outside the JSON array.\n";
        prompt += "CRITICAL: Return ONLY the JSON array containing the actual social media posts. DO NOT include the general content plan overview, context, pillars, or recommendations in the JSON output. Each item in the JSON array must be a final, ready-to-publish post intended for the channel subscribers.";

        return await GetTextCompletionAsync(prompt, "qwen/qwen3.6-plus", cancellationToken) ?? "Failed to generate content plan.";
    }

    public async Task<string> EditContentPlanAsync(string currentPlan, string userPrompt, CancellationToken cancellationToken = default)
    {
        var prompt = $"Current Plan:\n{currentPlan}\nUser requested changes: {userPrompt}\nPlease provide the updated plan in the same JSON format.";
        return await GetTextCompletionAsync(prompt, "qwen/qwen3.6-plus", cancellationToken) ?? "Failed to edit content plan.";
    }

    public async Task<string> GenerateImagePromptAsync(string postText, CancellationToken cancellationToken = default)
    {
        var prompt = $"Analyze the following social media post text and generate a highly detailed prompt (in English) for an AI image generation model (google/gemini-2.5-flash-image). The prompt should describe a scene that perfectly accompanies the post. IMPORTANT: I will also provide real photo references of the subject (e.g. a specific house, building, or location) along with this prompt to the image model. Therefore, your generated prompt MUST instruct the image model to strictly base the main subject on the provided reference photos, matching its style, architecture, and features, while adding suitable lighting, atmosphere, and surroundings described in the post.\n\nPost text: '{postText}'\n\nReturn ONLY the English image generation prompt without any introductory text, quotes, or markdown formatting.";
        return await GetTextCompletionAsync(prompt, "qwen/qwen3.6-plus", cancellationToken) ?? "Failed to generate image prompt.";
    }

    public async Task<string?> GenerateImageAsync(string prompt, CancellationToken cancellationToken = default)
    {
        var contentItems = new List<object>
        {
            new { type = "text", text = prompt }
        };

        var referencesPath = Path.Combine(Directory.GetCurrentDirectory(), "PhotoReferences");
        if (!Directory.Exists(referencesPath))
        {
            // Fallback to project root if running from bin
            referencesPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "SmmBot.Infrastructure", "PhotoReferences");
        }
        if (Directory.Exists(referencesPath))
        {
            var files = Directory.GetFiles(referencesPath, "*.webp");
            foreach (var file in files)
            {
                var bytes = await File.ReadAllBytesAsync(file, cancellationToken);
                var base64 = Convert.ToBase64String(bytes);
                contentItems.Add(new
                {
                    type = "image_url",
                    image_url = new { url = $"data:image/webp;base64,{base64}" }
                });
            }
        }

        var requestBody = new
        {
            model = "google/gemini-2.5-flash-image",
            messages = new[]
            {
                new { role = "user", content = contentItems }
            },
            modalities = new List<string> {"image", "text"},
            // image_config = new {
            //     aspect_ratio = "3:4"
            // }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();
            
            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
            return result?.Choices?.FirstOrDefault()?.Message?.Images?.FirstOrDefault()?.ImageUrl?.Url;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate image from RouterAI.");
            return null;
        }
    }

    public async Task<string?> GenerateVideoAsync(string prompt, CancellationToken cancellationToken = default)
    {
        // RouterAI / standard providers usually don't support direct video generation through simple unified API yet.
        // Returning null or a mock URL for now, or you'd integrate with a specific provider like Runway / Sora.
        _logger.LogWarning("Video generation is not natively supported by the generic AI endpoint yet.");
        return null;
    }

    private async Task<string?> GetTextCompletionAsync(string prompt, string model, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = model,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("chat/completions", requestBody, cancellationToken);
            // response.EnsureSuccessStatusCode();
            var test = await response.Content.ReadAsStringAsync();
            var result = await response.Content.ReadFromJsonAsync<ChatCompletionResponse>(cancellationToken: cancellationToken);
            return result?.Choices?.FirstOrDefault()?.Message?.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get text completion from RouterAI.");
            return null;
        }
    }
}

public class ChatCompletionResponse
{
    [JsonPropertyName("choices")]
    public List<Choice> Choices { get; set; } = new();
}

public class Choice
{
    [JsonPropertyName("message")]
    public Message Message { get; set; } = new();
}

public class Message
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("images")]
    public List<ImageItem> Images { get; set; } = new();
}

public class ImageItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("image_url")]
    public ImageUrlData ImageUrl { get; set; } = new();
}

public class ImageUrlData
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}

public class ImageGenerationResponse
{
    [JsonPropertyName("data")]
    public List<ImageData> Data { get; set; } = new();
}

public class ImageData
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;
}
