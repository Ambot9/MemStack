namespace MemStack.Model;

public class FeatureMemoryPrepareRequirementRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Requirement { get; set; } = string.Empty;
    public List<string> Projects { get; set; } = [];
    public string? ProductName { get; set; }
    public string? CustomerName { get; set; }
    public List<string> Tags { get; set; } = [];
}
