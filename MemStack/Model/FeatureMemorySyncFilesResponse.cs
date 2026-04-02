namespace MemStack.Model;

public class FeatureMemorySyncFilesResponse
{
    public string Status { get; set; } = "prepared";
    public string FeatureExternalId { get; set; } = string.Empty;
    public string CommitMessage { get; set; } = string.Empty;
    public List<FeatureMemorySyncFile> Files { get; set; } = [];
}

public class FeatureMemorySyncFile
{
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
