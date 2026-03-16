using MemStack.Model;

namespace MemStack.Data;

public interface IFeatureMemoryRepository
{
    IReadOnlyList<FeatureMemory> GetAll();
    FeatureMemory? GetById(int id);
    FeatureMemory? GetByExternalFeatureId(string externalFeatureId);
    IReadOnlyList<FeatureMemory> Search(string query, string? productName = null, string? status = null, string? tags = null);
    FeatureMemory Add(FeatureMemory memory);
    FeatureMemory? Update(FeatureMemory memory);
    bool Delete(int id);
}
