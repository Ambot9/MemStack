using System.ComponentModel.DataAnnotations;

namespace MemStack.Model;

/// <summary>
/// Used for POST /api/feature-memories/{id}/sync-summary
/// Called by Nexwork when a feature is completed to store final summary.
/// </summary>
public class SyncSummaryRequest
{
    [Required]
    public string SummaryMarkdown { get; set; } = string.Empty;
}

