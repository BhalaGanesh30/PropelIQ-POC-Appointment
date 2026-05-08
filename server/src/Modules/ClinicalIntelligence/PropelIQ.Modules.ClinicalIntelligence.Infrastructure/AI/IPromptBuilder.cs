using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Builds a structured chat completion request for clinical entity extraction.
/// The prompt includes a system message defining the task and output JSON schema,
/// few-shot examples, and the PII-redacted text chunk as the user message.
/// </summary>
public interface IPromptBuilder
{
    /// <summary>
    /// Constructs a <see cref="ChatMessage"/> list ready to be sent to the AI gateway.
    /// </summary>
    /// <param name="textChunk">PII-redacted text fragment to extract facts from.</param>
    /// <param name="modelId">Model alias to populate in the request.</param>
    /// <param name="maxTokens">Token budget ceiling for the completion.</param>
    IReadOnlyList<ChatMessage> BuildExtractionMessages(
        string textChunk,
        string modelId,
        int maxTokens);
}
