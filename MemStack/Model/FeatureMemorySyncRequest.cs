namespace MemStack.Model;

public class FeatureMemorySyncRequest
{
    public string EventName { get; set; } = string.Empty;
    public FeatureSyncPayload Feature { get; set; } = new();
    public Dictionary<string, object>? PluginData { get; set; }
    public string WorkspaceRoot { get; set; } = string.Empty;
    public StorageTargetPayload? StorageTarget { get; set; }
    public GitAccountPayload? GitAccount { get; set; }
    public string? FeatureName { get; set; }
    public string? ProjectName { get; set; }
    public string? Status { get; set; }
}

public class FeatureSyncPayload
{
    public string Name { get; set; } = string.Empty;
    public string CreatedAt { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
    public string? CompletedAt { get; set; }
    public string? ExpiresAt { get; set; }
    public List<FeatureSyncProjectPayload> Projects { get; set; } = [];
    public Dictionary<string, object>? PluginRefs { get; set; }
}

public class FeatureSyncProjectPayload
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Branch { get; set; } = string.Empty;
    public string? BaseBranch { get; set; }
    public string? WorktreePath { get; set; }
    public string? LastUpdated { get; set; }
    public List<string> ChangedFiles { get; set; } = [];
}

public class StorageTargetPayload
{
    public string Repository { get; set; } = string.Empty;
    public string Branch { get; set; } = "main";
    public string Path { get; set; } = "Features";
}

public class GitAccountPayload
{
    public string Provider { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string GitlabUrl { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
}
