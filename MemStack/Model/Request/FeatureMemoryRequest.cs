using System.ComponentModel.DataAnnotations;

namespace MemStack.Model;

public class FeatureMemoryRequest
{
    [Required, MaxLength(100)]
    public string ExternalFeatureId { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string SourceSystem { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string ProductName { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string CustomerName { get; set; } = string.Empty;

    [Required]
    public string RequirementMarkdown { get; set; } = string.Empty;

    public string ImplementationMarkdown { get; set; } = string.Empty;

    public string SummaryMarkdown { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Status { get; set; } = string.Empty;

    // Comma-separated tags e.g. "promotion,checkout"
    public string Tags { get; set; } = string.Empty;
}
