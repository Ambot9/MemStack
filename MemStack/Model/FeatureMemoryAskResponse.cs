namespace MemStack.Model;

public class FeatureMemoryAskResponse
{
    public string Status { get; set; } = "no_match";
    public string Verdict { get; set; } = "uncertain";
    public string Answer { get; set; } = string.Empty;
    public List<string> Why { get; set; } = [];
    public List<string> RelatedProjects { get; set; } = [];
    public List<string> PossibleChecks { get; set; } = [];
    public List<FeatureMemoryAskSource> Sources { get; set; } = [];
    public string Confidence { get; set; } = "low";
}

public class FeatureMemoryAskSource
{
    public int FeatureMemoryId { get; set; }
    public string FeatureTitle { get; set; } = string.Empty;
    public string FeatureExternalId { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
}
