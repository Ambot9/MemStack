using MemStack.Model;

namespace MemStack.Data;

public interface IGitRepository
{
    /// <summary>
    /// Initialize git repository if it doesn't exist.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Commit markdown files for a feature memory.
    /// </summary>
    void CommitFeatureMemory(
        FeatureMemory memory,
        string operation = "upsert",
        StorageTargetPayload? storageTarget = null,
        GitAccountPayload? gitAccount = null);

    List<FeatureMemorySyncFile> BuildFeatureFiles(
        FeatureMemory memory,
        StorageTargetPayload? storageTarget = null);

    /// <summary>
    /// Remove and commit deletion of feature memory files.
    /// </summary>
    void DeleteFeatureMemory(FeatureMemory memory);

    /// <summary>
    /// Check if git persistence is enabled.
    /// </summary>
    bool IsEnabled { get; }
}
