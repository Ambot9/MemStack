namespace MemStack.Model;

public class FeatureMemoryPrepareRequirementResponse
{
    public string Status { get; set; } = "prepared";
    public string Summary { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = [];
    public List<string> Projects { get; set; } = [];
}
