namespace MemStack.Model;

/// <summary>
/// Used for PATCH /api/feature-memories/{id}
/// Only updates the fields that are provided (non-null).
/// Guide example: update implementationMarkdown and status on feature completion.
/// </summary>
public class FeatureMemoryPatchRequest
{
    public string? RequirementMarkdown { get; set; }
    public string? ImplementationMarkdown { get; set; }
    public string? SummaryMarkdown { get; set; }
    public string? Status { get; set; }
    public string? Tags { get; set; }
}

