namespace MemStack.Model;

public class FeatureMemoryAskRequest
{
    public string Question { get; set; } = string.Empty;
    public List<string> Projects { get; set; } = [];
    public string? ProductName { get; set; }
    public string? FeatureName { get; set; }
    public List<string> Tags { get; set; } = [];
}
