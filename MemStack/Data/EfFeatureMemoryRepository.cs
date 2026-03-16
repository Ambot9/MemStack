using MemStack.Model;
using Microsoft.EntityFrameworkCore;

namespace MemStack.Data;

public class EfFeatureMemoryRepository(MemStackDbContext db) : IFeatureMemoryRepository
{
    public IReadOnlyList<FeatureMemory> GetAll()
    {
        return db.FeatureMemories.AsNoTracking().ToList();
    }

    public FeatureMemory? GetById(int id)
    {
        return db.FeatureMemories.AsNoTracking().FirstOrDefault(x => x.Id == id);
    }

    public FeatureMemory? GetByExternalFeatureId(string externalFeatureId)
    {
        return db.FeatureMemories.AsNoTracking().FirstOrDefault(x => x.ExternalFeatureId == externalFeatureId);
    }

    public IReadOnlyList<FeatureMemory> Search(string query, string? productName = null, string? status = null, string? tags = null)
    {
        var normalizedQuery = query.Trim().ToLowerInvariant();
        var normalizedProduct = productName?.Trim().ToLowerInvariant();
        var normalizedStatus = status?.Trim().ToLowerInvariant();
        var normalizedTags = tags?.Trim().ToLowerInvariant();

        var dbQuery = db.FeatureMemories.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedProduct))
        {
            dbQuery = dbQuery.Where(x => x.ProductName.ToLower().Contains(normalizedProduct));
        }

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            dbQuery = dbQuery.Where(x => x.Status.ToLower() == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(normalizedTags))
        {
            dbQuery = dbQuery.Where(x => x.Tags.ToLower().Contains(normalizedTags));
        }

        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return dbQuery.OrderByDescending(x => x.UpdatedAtUtc).Take(50).ToList();
        }

        return dbQuery
            .Where(x =>
                x.ExternalFeatureId.ToLower().Contains(normalizedQuery) ||
                x.Title.ToLower().Contains(normalizedQuery) ||
                x.ProductName.ToLower().Contains(normalizedQuery) ||
                x.CustomerName.ToLower().Contains(normalizedQuery) ||
                x.RequirementMarkdown.ToLower().Contains(normalizedQuery) ||
                x.ImplementationMarkdown.ToLower().Contains(normalizedQuery) ||
                x.SummaryMarkdown.ToLower().Contains(normalizedQuery) ||
                x.Tags.ToLower().Contains(normalizedQuery))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Take(50)
            .ToList();
    }

    public FeatureMemory Add(FeatureMemory memory)
    {
        db.FeatureMemories.Add(memory);
        db.SaveChanges();
        return memory;
    }

    public FeatureMemory? Update(FeatureMemory memory)
    {
        var existing = db.FeatureMemories.FirstOrDefault(x => x.Id == memory.Id);
        if (existing is null)
        {
            return null;
        }

        existing.ExternalFeatureId = memory.ExternalFeatureId;
        existing.SourceSystem = memory.SourceSystem;
        existing.Title = memory.Title;
        existing.ProductName = memory.ProductName;
        existing.CustomerName = memory.CustomerName;
        existing.RequirementMarkdown = memory.RequirementMarkdown;
        existing.ImplementationMarkdown = memory.ImplementationMarkdown;
        existing.SummaryMarkdown = memory.SummaryMarkdown;
        existing.Status = memory.Status;
        existing.Tags = memory.Tags;
        existing.UpdatedAtUtc = memory.UpdatedAtUtc;

        db.SaveChanges();
        return existing;
    }

    public bool Delete(int id)
    {
        var existing = db.FeatureMemories.FirstOrDefault(x => x.Id == id);
        if (existing is null)
        {
            return false;
        }

        db.FeatureMemories.Remove(existing);
        db.SaveChanges();
        return true;
    }
}
