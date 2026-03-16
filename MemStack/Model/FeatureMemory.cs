namespace MemStack.Model;

public class FeatureMemory
{
    public int Id { get; set; }
    public string ExternalFeatureId { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string RequirementMarkdown { get; set; } = string.Empty;
    public string ImplementationMarkdown { get; set; } = string.Empty;
    public string SummaryMarkdown { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    // Comma-separated tags e.g. "promotion,checkout"
    public string Tags { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
