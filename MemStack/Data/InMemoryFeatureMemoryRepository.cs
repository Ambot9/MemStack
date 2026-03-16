using MemStack.Model;

namespace MemStack.Data;

public class InMemoryFeatureMemoryRepository : IFeatureMemoryRepository
{
    private readonly List<FeatureMemory> _items = new();
    private int _nextId = 1;
    private readonly Lock _gate = new();

    public IReadOnlyList<FeatureMemory> GetAll()
    {
        lock (_gate)
        {
            return _items.Select(Clone).ToList();
        }
    }

    public FeatureMemory? GetById(int id)
    {
        lock (_gate)
        {
            var found = _items.FirstOrDefault(x => x.Id == id);
            return found is null ? null : Clone(found);
        }
    }

    public FeatureMemory? GetByExternalFeatureId(string externalFeatureId)
    {
        lock (_gate)
        {
            var found = _items.FirstOrDefault(x => x.ExternalFeatureId == externalFeatureId);
            return found is null ? null : Clone(found);
        }
    }

    public IReadOnlyList<FeatureMemory> Search(string query, string? productName = null, string? status = null, string? tags = null)
    {
        lock (_gate)
        {
            var normalizedQuery = query.Trim().ToLowerInvariant();
            var normalizedProduct = productName?.Trim().ToLowerInvariant();
            var normalizedStatus = status?.Trim().ToLowerInvariant();
            var normalizedTags = tags?.Trim().ToLowerInvariant();

            IEnumerable<FeatureMemory> results = _items;

            if (!string.IsNullOrWhiteSpace(normalizedProduct))
            {
                results = results.Where(x => x.ProductName.ToLowerInvariant().Contains(normalizedProduct));
            }

            if (!string.IsNullOrWhiteSpace(normalizedStatus))
            {
                results = results.Where(x => x.Status.ToLowerInvariant() == normalizedStatus);
            }

            if (!string.IsNullOrWhiteSpace(normalizedTags))
            {
                results = results.Where(x => x.Tags.ToLowerInvariant().Contains(normalizedTags));
            }

            if (!string.IsNullOrWhiteSpace(normalizedQuery))
            {
                results = results.Where(x =>
                    x.ExternalFeatureId.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.Title.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.ProductName.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.CustomerName.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.RequirementMarkdown.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.ImplementationMarkdown.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.SummaryMarkdown.ToLowerInvariant().Contains(normalizedQuery) ||
                    x.Tags.ToLowerInvariant().Contains(normalizedQuery));
            }

            return results
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(50)
                .Select(Clone)
                .ToList();
        }
    }

    public FeatureMemory Add(FeatureMemory memory)
    {
        lock (_gate)
        {
            memory.Id = _nextId++;
            var toStore = Clone(memory);
            _items.Add(toStore);
            return Clone(toStore);
        }
    }

    public FeatureMemory? Update(FeatureMemory memory)
    {
        lock (_gate)
        {
            var index = _items.FindIndex(x => x.Id == memory.Id);
            if (index < 0)
            {
                return null;
            }

            _items[index] = Clone(memory);
            return Clone(_items[index]);
        }
    }

    public bool Delete(int id)
    {
        lock (_gate)
        {
            var removed = _items.RemoveAll(x => x.Id == id);
            return removed > 0;
        }
    }

    private static FeatureMemory Clone(FeatureMemory source)
    {
        return new FeatureMemory
        {
            Id = source.Id,
            ExternalFeatureId = source.ExternalFeatureId,
            SourceSystem = source.SourceSystem,
            Title = source.Title,
            ProductName = source.ProductName,
            CustomerName = source.CustomerName,
            RequirementMarkdown = source.RequirementMarkdown,
            ImplementationMarkdown = source.ImplementationMarkdown,
            SummaryMarkdown = source.SummaryMarkdown,
            Status = source.Status,
            Tags = source.Tags,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc
        };
    }
}
