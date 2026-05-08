using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using PropelIQ.SharedKernel.AiGateway.Models;

namespace PropelIQ.Modules.ClinicalIntelligence.Infrastructure.AI;

/// <summary>
/// Builds a structured extraction prompt using the system template and few-shot
/// examples embedded in the assembly (<c>Prompts/clinical-extraction/</c>).
///
/// Token budget: system + examples occupy ≈ 800 tokens, leaving the configured
/// <c>MaxChunkSize</c> for user content and the remainder for completion (AIR-001).
/// </summary>
public sealed class ExtractionPromptBuilder : IPromptBuilder
{
    private static readonly string SystemPrompt;
    private static readonly string FewShotBlock;

    static ExtractionPromptBuilder()
    {
        var assembly   = typeof(ExtractionPromptBuilder).Assembly;
        SystemPrompt   = ReadEmbeddedResource(assembly, "system.txt");
        FewShotBlock   = BuildFewShotBlock(assembly);
    }

    private readonly ILogger<ExtractionPromptBuilder> _logger;

    public ExtractionPromptBuilder(ILogger<ExtractionPromptBuilder> logger) => _logger = logger;

    /// <inheritdoc />
    public IReadOnlyList<ChatMessage> BuildExtractionMessages(
        string textChunk,
        string modelId,
        int maxTokens)
    {
        _logger.LogDebug(
            "Building extraction prompt for model={Model} chunkLength={Length}.",
            modelId, textChunk.Length);

        return
        [
            new ChatMessage { Role = "system",    Content = SystemPrompt },
            new ChatMessage { Role = "user",      Content = FewShotBlock },
            new ChatMessage { Role = "assistant", Content = "Understood. I will extract facts from the clinical text and return only the JSON object." },
            new ChatMessage { Role = "user",      Content = textChunk },
        ];
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static string ReadEmbeddedResource(Assembly assembly, string fileName)
    {
        // Resource names use dots, not slashes; namespace prefix included.
        var resourceName = assembly
            .GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
        {
            // Fall back to constant defaults if not embedded (e.g. unit-test projects).
            return fileName == "system.txt" ? DefaultSystemPrompt : string.Empty;
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string BuildFewShotBlock(Assembly assembly)
    {
        var json = ReadEmbeddedResource(assembly, "examples.json");
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        try
        {
            using var doc     = JsonDocument.Parse(json);
            var sb            = new StringBuilder("Here are examples of the expected extraction format:\n\n");
            int exampleNumber = 1;

            foreach (var example in doc.RootElement.EnumerateArray())
            {
                var input  = example.GetProperty("input").GetString();
                var output = example.GetProperty("output").GetRawText();
                sb.AppendLine($"Example {exampleNumber++}:");
                sb.AppendLine($"Input: {input}");
                sb.AppendLine($"Output: {output}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
        catch
        {
            // Malformed examples.json — skip few-shot; system prompt alone is sufficient.
            return string.Empty;
        }
    }

    private const string DefaultSystemPrompt =
        "You are a clinical entity extraction specialist. " +
        "Extract structured medical facts and return ONLY a JSON object with a \"facts\" array. " +
        "Each fact must have: fact_type (medication|allergy|diagnosis|finding), " +
        "name, value, confidence (0.0-1.0), source_text.";
}
