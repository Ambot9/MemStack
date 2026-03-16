namespace MemStack.Model;

public class FeatureMemorySearchRequest
{
    public string Query { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? Status { get; set; }
    public string? Tags { get; set; }
}
