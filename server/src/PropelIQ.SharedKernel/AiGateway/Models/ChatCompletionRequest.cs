using System.Text.Json.Serialization;

namespace PropelIQ.SharedKernel.AiGateway.Models;

/// <summary>
/// OpenAI-compatible chat completion request sent to the LiteLLM proxy.
/// Property names are serialized as snake_case to match the OpenAI API contract
/// (configured via JsonNamingPolicy.SnakeCaseLower on the shared JsonSerializerOptions).
/// </summary>
public sealed record ChatCompletionRequest
{
    /// <summary>Model alias defined in the LiteLLM proxy config.yaml (e.g. "coding-suggestion").</summary>
    public required string Model { get; init; }

    /// <summary>Conversation turns sent to the model.</summary>
    public required IReadOnlyList<ChatMessage> Messages { get; init; }

    /// <summary>Sampling temperature — lower is more deterministic.</summary>
    public double Temperature { get; init; } = 0.7;

    /// <summary>Optional upper bound on generated tokens. Null = model default.</summary>
    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }
}

/// <summary>A single conversation turn (role + content).</summary>
public sealed record ChatMessage
{
    /// <summary>One of: "system", "user", "assistant".</summary>
    public required string Role { get; init; }

    /// <summary>Text content of the message.</summary>
    public required string Content { get; init; }
}
