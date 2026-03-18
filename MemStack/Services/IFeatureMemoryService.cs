using MemStack.Model;

namespace MemStack.Services;

public interface IFeatureMemoryService
{
    IReadOnlyList<FeatureMemoryResponse> GetAll();
    FeatureMemoryResponse? GetById(int id);
    IReadOnlyList<FeatureMemoryResponse> Search(FeatureMemorySearchRequest request);
    FeatureMemoryPrepareRequirementResponse PrepareRequirement(FeatureMemoryPrepareRequirementRequest request);
    FeatureMemoryAskResponse Ask(FeatureMemoryAskRequest request);
    FeatureMemoryResponse SyncFromNexwork(FeatureMemorySyncRequest request);
    FeatureMemoryResponse Create(FeatureMemoryRequest request);
    FeatureMemoryResponse? Update(int id, FeatureMemoryRequest request);
    FeatureMemoryResponse? Patch(int id, FeatureMemoryPatchRequest request);
    FeatureMemoryResponse? SyncSummary(int id, string summaryMarkdown);
    bool Delete(int id);
    bool IsValidStatus(string status);
}
