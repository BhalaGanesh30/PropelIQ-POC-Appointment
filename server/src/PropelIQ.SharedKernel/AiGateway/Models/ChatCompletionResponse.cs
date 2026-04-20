namespace PropelIQ.SharedKernel.AiGateway.Models;

/// <summary>
/// OpenAI-compatible chat completion response returned by the LiteLLM proxy.
/// Property names are deserialized from snake_case using JsonNamingPolicy.SnakeCaseLower.
/// </summary>
public sealed record ChatCompletionResponse
{
    /// <summary>Provider-assigned completion ID.</summary>
    public required string Id { get; init; }

    /// <summary>Model name as reported by the provider (may differ from the requested alias).</summary>
    public required string Model { get; init; }

    /// <summary>One or more generated completion choices.</summary>
    public required IReadOnlyList<Choice> Choices { get; init; }

    /// <summary>Token usage statistics for billing and observability (AC-3).</summary>
    public required UsageInfo Usage { get; init; }
}

/// <summary>A single generated completion choice.</summary>
public sealed record Choice
{
    /// <summary>The assistant message produced by the model.</summary>
    public required ChatMessage Message { get; init; }

    /// <summary>Reason the model stopped generating: "stop", "length", etc.</summary>
    public string? FinishReason { get; init; }
}

/// <summary>
/// Token usage breakdown for the completion.
/// Emitted as OTel span attributes for cost and latency observability (AC-3).
/// </summary>
public sealed record UsageInfo
{
    /// <summary>Tokens consumed by the input (system + user messages).</summary>
    public int PromptTokens { get; init; }

    /// <summary>Tokens produced by the model in the response.</summary>
    public int CompletionTokens { get; init; }

    /// <summary>Total tokens billed (prompt + completion).</summary>
    public int TotalTokens { get; init; }
}
