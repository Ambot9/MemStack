using MemStack.Data;
using MemStack.Model;
using System.Text.Json;

namespace MemStack.Services;

public class FeatureMemoryService(
    IFeatureMemoryRepository repository,
    IGitRepository gitRepository) : IFeatureMemoryService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Planned", "InProgress", "Done", "Blocked"
    };

    public IReadOnlyList<FeatureMemoryResponse> GetAll() => repository.GetAll().Select(MapToResponse).ToList();

    public FeatureMemoryResponse? GetById(int id)
    {
        var item = repository.GetById(id);
        return item is null ? null : MapToResponse(item);
    }

    public IReadOnlyList<FeatureMemoryResponse> Search(FeatureMemorySearchRequest request)
    {
        return repository
            .Search(request.Query, request.ProductName, request.Status, request.Tags)
            .Select(MapToResponse)
            .ToList();
    }

    public FeatureMemoryPrepareRequirementResponse PrepareRequirement(FeatureMemoryPrepareRequirementRequest request)
    {
        var title = request.Title.Trim();
        var description = request.Description.Trim();
        var requirement = request.Requirement.Trim();
        var productName = request.ProductName?.Trim();
        var customerName = request.CustomerName?.Trim();
        var projects = request.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project))
            .Select(project => project.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var tags = request.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag)
            .ToList();

        var lines = new List<string>();

        if (!string.IsNullOrWhiteSpace(title))
        {
            lines.Add($"# {title}");
            lines.Add(string.Empty);
        }

        lines.Add("## Objective");
        lines.Add(!string.IsNullOrWhiteSpace(requirement)
            ? requirement
            : !string.IsNullOrWhiteSpace(description)
                ? description
                : "Capture the intended feature behavior and expected user outcome.");

        if (!string.IsNullOrWhiteSpace(description) &&
            !string.Equals(description, requirement, StringComparison.OrdinalIgnoreCase))
        {
            lines.Add(string.Empty);
            lines.Add("## Context");
            lines.Add(description);
        }

        if (!string.IsNullOrWhiteSpace(productName) || !string.IsNullOrWhiteSpace(customerName) || projects.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Scope");

            if (!string.IsNullOrWhiteSpace(productName))
            {
                lines.Add($"- Product: {productName}");
            }

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                lines.Add($"- Customer: {customerName}");
            }

            if (projects.Count > 0)
            {
                lines.Add($"- Related projects: {string.Join(", ", projects)}");
            }
        }

        if (tags.Count > 0)
        {
            lines.Add(string.Empty);
            lines.Add("## Tags");
            lines.Add(string.Join(", ", tags));
        }

        return new FeatureMemoryPrepareRequirementResponse
        {
            Status = "prepared",
            Summary = string.Join(Environment.NewLine, lines).Trim(),
            Tags = tags,
            Projects = projects
        };
    }

    public FeatureMemoryAskResponse Ask(FeatureMemoryAskRequest request)
    {
        var trimmedQuestion = request.Question.Trim();
        var normalizedProjects = request.Projects
            .Where(project => !string.IsNullOrWhiteSpace(project))
            .Select(project => project.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var normalizedTags = request.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var candidates = repository.GetAll();

        if (!string.IsNullOrWhiteSpace(request.ProductName))
        {
            var productName = request.ProductName.Trim();
            candidates = candidates
                .Where(item => item.ProductName.Contains(productName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(request.FeatureName))
        {
            var featureName = request.FeatureName.Trim();
            candidates = candidates
                .Where(item =>
                    item.Title.Contains(featureName, StringComparison.OrdinalIgnoreCase) ||
                    item.ExternalFeatureId.Contains(featureName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (normalizedTags.Count > 0)
        {
            candidates = candidates
                .Where(item => normalizedTags.Any(tag => item.Tags.Contains(tag, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var ranked = candidates
            .Select(item => new
            {
                Item = item,
                Score = ScoreFeatureMemory(item, trimmedQuestion, normalizedProjects, normalizedTags)
            })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenByDescending(entry => entry.Item.UpdatedAtUtc)
            .Take(3)
            .ToList();

        if (ranked.Count == 0)
        {
            return new FeatureMemoryAskResponse
            {
                Status = "no_match",
                Verdict = "uncertain",
                Answer = "I could not find a matching feature memory record for this question.",
                Why = ["No stored requirement or implementation notes matched the provided question and filters."],
                RelatedProjects = normalizedProjects,
                PossibleChecks =
                [
                    "Add more project names if you know which systems are involved.",
                    "Try a more specific question with a promotion code, feature name, or ticket ID."
                ],
                Confidence = "low"
            };
        }

        var topMatch = ranked[0].Item;
        var sources = BuildSources(ranked.Select(entry => entry.Item).ToList(), trimmedQuestion, normalizedProjects);
        var why = BuildWhy(topMatch, normalizedProjects);
        var possibleChecks = BuildPossibleChecks(topMatch, normalizedProjects, trimmedQuestion);

        return new FeatureMemoryAskResponse
        {
            Status = "answered",
            Verdict = DeriveVerdict(topMatch, trimmedQuestion),
            Answer = BuildAnswer(topMatch),
            Why = why,
            RelatedProjects = normalizedProjects,
            PossibleChecks = possibleChecks,
            Sources = sources,
            Confidence = DeriveConfidence(ranked[0].Score, ranked.Count)
        };
    }

    public FeatureMemoryResponse SyncFromNexwork(FeatureMemorySyncRequest request)
    {
        var persisted = PersistFromNexwork(request, commitToGit: true);
        return MapToResponse(persisted);
    }

    public FeatureMemorySyncFilesResponse PrepareSyncFiles(FeatureMemorySyncRequest request)
    {
        var persisted = PersistFromNexwork(request, commitToGit: false);
        var files = gitRepository.BuildFeatureFiles(persisted, request.StorageTarget);

        return new FeatureMemorySyncFilesResponse
        {
            Status = "prepared",
            FeatureExternalId = persisted.ExternalFeatureId,
            CommitMessage = $"docs(memstack): sync {persisted.ExternalFeatureId}",
            Files = files
        };
    }

    private FeatureMemory PersistFromNexwork(FeatureMemorySyncRequest request, bool commitToGit)
    {
        var now = DateTime.UtcNow;
        var existing = repository.GetByExternalFeatureId(request.Feature.Name);
        var memstackData = request.PluginData is not null && request.PluginData.TryGetValue("memstack", out var rawMemstack)
            ? ToDictionary(rawMemstack)
            : null;

        var item = existing ?? new FeatureMemory
        {
            ExternalFeatureId = request.Feature.Name,
            SourceSystem = "nexwork",
            Title = request.Feature.Name,
            ProductName = InferProductName(request, memstackData),
            CustomerName = "Nexwork",
            CreatedAtUtc = ParseDateOrFallback(request.Feature.CreatedAt, now),
        };

        item.ExternalFeatureId = request.Feature.Name;
        item.SourceSystem = "nexwork";
        item.Title = request.Feature.Name;
        item.ProductName = ExtractString(memstackData, "productName", item.ProductName, InferProductName(request, memstackData));
        item.CustomerName = ExtractString(memstackData, "customerName", item.CustomerName, "Nexwork");
        item.RequirementMarkdown = ExtractString(memstackData, "requirement", item.RequirementMarkdown, item.RequirementMarkdown);
        item.SummaryMarkdown = BuildSummaryMarkdown(request, memstackData, item);
        item.ImplementationMarkdown = BuildImplementationSection(request, item);
        item.Tags = BuildTags(request, memstackData, item.Tags);
        item.Status = NormalizeStatus(MapFeatureStatus(request, item.Status));
        item.UpdatedAtUtc = ParseDateOrFallback(request.Feature.UpdatedAt, now);

        FeatureMemory persisted;
        if (existing is null)
        {
            persisted = repository.Add(item);
        }
        else
        {
            persisted = repository.Update(item) ?? item;
        }

        if (commitToGit)
        {
            gitRepository.CommitFeatureMemory(
                persisted,
                request.EventName,
                request.StorageTarget,
                request.GitAccount);
        }

        return persisted;
    }

    public FeatureMemoryResponse Create(FeatureMemoryRequest request)
    {
        var now = DateTime.UtcNow;
        var item = new FeatureMemory
        {
            ExternalFeatureId = request.ExternalFeatureId.Trim(),
            SourceSystem = request.SourceSystem.Trim(),
            Title = request.Title.Trim(),
            ProductName = request.ProductName.Trim(),
            CustomerName = request.CustomerName.Trim(),
            RequirementMarkdown = request.RequirementMarkdown,
            ImplementationMarkdown = request.ImplementationMarkdown,
            SummaryMarkdown = request.SummaryMarkdown,
            Status = NormalizeStatus(request.Status),
            Tags = request.Tags.Trim(),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        var created = repository.Add(item);
        gitRepository.CommitFeatureMemory(created, "create");
        return MapToResponse(created);
    }

    public FeatureMemoryResponse? Update(int id, FeatureMemoryRequest request)
    {
        var existing = repository.GetById(id);
        if (existing is null)
        {
            return null;
        }

        existing.ExternalFeatureId = request.ExternalFeatureId.Trim();
        existing.SourceSystem = request.SourceSystem.Trim();
        existing.Title = request.Title.Trim();
        existing.ProductName = request.ProductName.Trim();
        existing.CustomerName = request.CustomerName.Trim();
        existing.RequirementMarkdown = request.RequirementMarkdown;
        existing.ImplementationMarkdown = request.ImplementationMarkdown;
        existing.SummaryMarkdown = request.SummaryMarkdown;
        existing.Status = NormalizeStatus(request.Status);
        existing.Tags = request.Tags.Trim();
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = repository.Update(existing);
        if (updated is not null)
        {
            gitRepository.CommitFeatureMemory(updated, "update");
        }
        return updated is null ? null : MapToResponse(updated);
    }

    // PATCH: only update the fields that are provided (non-null)
    public FeatureMemoryResponse? Patch(int id, FeatureMemoryPatchRequest request)
    {
        var existing = repository.GetById(id);
        if (existing is null) return null;

        if (request.RequirementMarkdown is not null)
            existing.RequirementMarkdown = request.RequirementMarkdown;
        if (request.ImplementationMarkdown is not null)
            existing.ImplementationMarkdown = request.ImplementationMarkdown;
        if (request.SummaryMarkdown is not null)
            existing.SummaryMarkdown = request.SummaryMarkdown;
        if (request.Status is not null)
            existing.Status = NormalizeStatus(request.Status);
        if (request.Tags is not null)
            existing.Tags = request.Tags.Trim();

        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = repository.Update(existing);
        if (updated is not null)
            gitRepository.CommitFeatureMemory(updated, "patch");
        return updated is null ? null : MapToResponse(updated);
    }

    // sync-summary: triggered by Nexwork when a feature is completed (guide Step 6 + Step 7)
    public FeatureMemoryResponse? SyncSummary(int id, string summaryMarkdown)
    {
        var existing = repository.GetById(id);
        if (existing is null) return null;

        existing.SummaryMarkdown = summaryMarkdown;
        existing.UpdatedAtUtc = DateTime.UtcNow;

        var updated = repository.Update(existing);
        if (updated is not null)
            gitRepository.CommitFeatureMemory(updated, "sync-summary");
        return updated is null ? null : MapToResponse(updated);
    }

    public bool Delete(int id)
    {        var existing = repository.GetById(id);
        var deleted = repository.Delete(id);
        if (deleted && existing is not null)
        {
            gitRepository.DeleteFeatureMemory(existing);
        }
        return deleted;
    }

    public bool IsValidStatus(string status) => AllowedStatuses.Contains(status.Trim());

    private static string NormalizeStatus(string status)
    {
        var trimmed = status.Trim();
        return AllowedStatuses.First(x => x.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
    }

    private static FeatureMemoryResponse MapToResponse(FeatureMemory item)
    {
        return new FeatureMemoryResponse
        {
            Id = item.Id,
            ExternalFeatureId = item.ExternalFeatureId,
            SourceSystem = item.SourceSystem,
            Title = item.Title,
            ProductName = item.ProductName,
            CustomerName = item.CustomerName,
            RequirementMarkdown = item.RequirementMarkdown,
            ImplementationMarkdown = item.ImplementationMarkdown,
            SummaryMarkdown = item.SummaryMarkdown,
            Status = item.Status,
            Tags = item.Tags,
            CreatedAtUtc = item.CreatedAtUtc,
            UpdatedAtUtc = item.UpdatedAtUtc
        };
    }

    private static string BuildSummaryMarkdown(
        FeatureMemorySyncRequest request,
        Dictionary<string, object?>? memstackData,
        FeatureMemory current)
    {
        var requirementSummary = ExtractString(memstackData, "requirementSummary", current.SummaryMarkdown, string.Empty);
        if (!string.IsNullOrWhiteSpace(requirementSummary) && request.EventName == "feature.created")
        {
            return requirementSummary;
        }

        if (!string.IsNullOrWhiteSpace(current.SummaryMarkdown))
        {
            return current.SummaryMarkdown;
        }

        var lines = new List<string>
        {
            $"Feature `{request.Feature.Name}` synced from Nexwork.",
            $"Lifecycle event: {request.EventName}.",
        };

        if (request.Feature.Projects.Count > 0)
        {
            lines.Add("Projects:");
            lines.AddRange(request.Feature.Projects.Select(project => $"- {project.Name}: {project.Status}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildImplementationSection(FeatureMemorySyncRequest request, FeatureMemory current)
    {
        var lines = new List<string>
        {
            $"Sync event: {request.EventName}",
            $"Updated at: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC",
        };

        if (!string.IsNullOrWhiteSpace(request.ProjectName) && !string.IsNullOrWhiteSpace(request.Status))
        {
            lines.Add($"Changed project: {request.ProjectName} -> {request.Status}");
        }

        if (request.Feature.Projects.Count > 0)
        {
            lines.Add("Projects:");
            lines.AddRange(request.Feature.Projects.Select(project =>
                $"- {project.Name}: status={project.Status}, branch={project.Branch}, base={project.BaseBranch ?? "unknown"}"));
        }

        var codeAreaLines = request.Feature.Projects
            .Where(project => project.ChangedFiles.Count > 0)
            .SelectMany(project => project.ChangedFiles.Select(file => $"- {project.Name}: {file}"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codeAreaLines.Count > 0)
        {
            lines.Add("Code Areas:");
            lines.AddRange(codeAreaLines);
        }

        var methodEvidenceLines = request.Feature.Projects
            .SelectMany(project => project.CodeSymbols.SelectMany(entry =>
                entry.Symbols
                    .Where(symbol => string.Equals(symbol.Kind, "method", StringComparison.OrdinalIgnoreCase))
                    .Select(symbol => $"- {project.Name}: {symbol.Name}")))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (methodEvidenceLines.Count > 0)
        {
            lines.Add("Method Evidence:");
            lines.AddRange(methodEvidenceLines);
        }

        var handledFlows = InferHandledFlows(request.Feature);
        if (handledFlows.Count > 0)
        {
            lines.Add("Handled Flows:");
            lines.AddRange(handledFlows.Select(flow => $"- {flow}"));
        }

        var newSection = string.Join(Environment.NewLine, lines);
        if (string.IsNullOrWhiteSpace(current.ImplementationMarkdown))
            return newSection;

        var todayPrefix = $"Sync event: {request.EventName}{Environment.NewLine}Updated at: {DateTime.UtcNow:yyyy-MM-dd}";
        if (current.ImplementationMarkdown.Contains(todayPrefix, StringComparison.OrdinalIgnoreCase))
            return current.ImplementationMarkdown;

        return $"{current.ImplementationMarkdown}{Environment.NewLine}{Environment.NewLine}---{Environment.NewLine}{newSection}";
    }

    private static string BuildTags(FeatureMemorySyncRequest request, Dictionary<string, object?>? memstackData, string existingTags)
    {
        var projectNames = new HashSet<string>(
            request.Feature.Projects.Select(project => project.Name),
            StringComparer.OrdinalIgnoreCase);

        var tags = new HashSet<string>(
            SplitCsv(existingTags).Where(tag => !projectNames.Contains(tag)),
            StringComparer.OrdinalIgnoreCase);

        foreach (var tag in SplitCsv(ExtractString(memstackData, "tags", string.Empty, string.Empty)))
        {
            tags.Add(tag);
        }

        foreach (var inferredTag in InferTopics(request, memstackData))
        {
            if (!projectNames.Contains(inferredTag))
            {
                tags.Add(inferredTag);
            }
        }

        return string.Join(", ", tags.OrderBy(tag => tag));
    }

    private static string InferProductName(FeatureSyncPayload feature)
    {
        var analysisText = string.Join(
            Environment.NewLine,
            feature.Name ?? string.Empty,
            feature.PluginRefs?.ToString() ?? string.Empty);

        if (ContainsAny(analysisText, "payment", "deposit", "withdraw", "provider", "currency", "bank", "merchant",
            "paygrid", "toppay", "abapay", "aba pay", "sudalink", "stripe"))
        {
            return "Payment";
        }

        if (ContainsAny(analysisText, "promotion", "bonus", "cashback", "first deposit", "referral", "rebate"))
        {
            return "Promotion";
        }

        return "General";
    }

    private static string InferProductName(FeatureMemorySyncRequest request, Dictionary<string, object?>? memstackData)
    {
        var analysisText = BuildAnalysisText(request, memstackData);

        if (ContainsAny(analysisText, "payment", "deposit", "withdraw", "provider", "currency", "bank", "merchant",
            "paygrid", "toppay", "abapay", "aba pay", "sudalink", "stripe"))
        {
            return "Payment";
        }

        if (ContainsAny(analysisText, "promotion", "bonus", "cashback", "first deposit", "referral", "rebate"))
        {
            return "Promotion";
        }

        return "General";
    }

    private static string MapFeatureStatus(FeatureMemorySyncRequest request, string fallback)
    {
        if (request.EventName == "feature.completed")
        {
            return "Done";
        }

        if (request.Feature.Projects.Count > 0 && request.Feature.Projects.All(project => string.Equals(project.Status, "completed", StringComparison.OrdinalIgnoreCase)))
        {
            return "Done";
        }

        if (request.Feature.Projects.Any(project => string.Equals(project.Status, "in_progress", StringComparison.OrdinalIgnoreCase)))
        {
            return "InProgress";
        }

        return string.IsNullOrWhiteSpace(fallback) ? "Planned" : fallback;
    }

    private static string ExtractString(Dictionary<string, object?>? source, string propertyName, string currentValue, string fallback)
    {
        if (source is not null && source.TryGetValue(propertyName, out var value))
        {
            if (TryReadString(value, out var stringValue) && !string.IsNullOrWhiteSpace(stringValue))
            {
                return stringValue.Trim();
            }

            if (TryReadStringList(value, out var listValue))
            {
                var joined = string.Join(", ", listValue.Select(item => item?.ToString()).Where(item => !string.IsNullOrWhiteSpace(item)));
                if (!string.IsNullOrWhiteSpace(joined))
                {
                    return joined;
                }
            }
        }

        return !string.IsNullOrWhiteSpace(currentValue) ? currentValue : fallback;
    }

    private static Dictionary<string, object?>? ToDictionary(object? value)
    {
        return value switch
        {
            null => null,
            Dictionary<string, object?> typedDictionary => typedDictionary,
            JsonElement element when element.ValueKind == JsonValueKind.Object => element
                .EnumerateObject()
                .ToDictionary(
                    property => property.Name,
                    property => NormalizeJsonValue(property.Value),
                    StringComparer.OrdinalIgnoreCase),
            _ => null
        };
    }

    private static object? NormalizeJsonValue(object? value)
    {
        return value switch
        {
            null => null,
            JsonElement element => element.ValueKind switch
            {
                JsonValueKind.Object => ToDictionary(element),
                JsonValueKind.Array => element.EnumerateArray().Select(item => NormalizeJsonValue(item)).ToList(),
                JsonValueKind.String => element.GetString(),
                JsonValueKind.Number => element.ToString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                _ => element.ToString()
            },
            _ => value
        };
    }

    private static bool TryReadString(object? value, out string result)
    {
        result = string.Empty;

        switch (value)
        {
            case null:
                return false;
            case string stringValue:
                result = stringValue;
                return true;
            case JsonElement element when element.ValueKind == JsonValueKind.String:
                result = element.GetString() ?? string.Empty;
                return true;
            default:
                return false;
        }
    }

    private static bool TryReadStringList(object? value, out List<string> result)
    {
        result = [];

        switch (value)
        {
            case IEnumerable<object?> listValue:
                result = listValue
                    .Select(item => item?.ToString() ?? string.Empty)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
                return result.Count > 0;
            case JsonElement element when element.ValueKind == JsonValueKind.Array:
                result = element
                    .EnumerateArray()
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .ToList();
                return result.Count > 0;
            default:
                return false;
        }
    }

    private static IEnumerable<string> SplitCsv(string? value)
    {
        return (value ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => !string.IsNullOrWhiteSpace(item));
    }

    private static List<string> InferHandledFlows(FeatureSyncPayload feature)
    {
        var flows = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var symbolName in feature.Projects
                     .SelectMany(project => project.CodeSymbols)
                     .SelectMany(entry => entry.Symbols)
                     .Select(symbol => symbol.Name))
        {
            if (symbolName.Contains("GetBankList", StringComparison.OrdinalIgnoreCase))
                flows.Add("GetBankList");
            if (symbolName.Contains("Deposit", StringComparison.OrdinalIgnoreCase))
                flows.Add("Deposit");
            if (symbolName.Contains("Withdraw", StringComparison.OrdinalIgnoreCase))
                flows.Add("Withdraw");
            if (symbolName.Contains("Callback", StringComparison.OrdinalIgnoreCase) ||
                symbolName.Contains("Notify", StringComparison.OrdinalIgnoreCase))
                flows.Add("Callback");
        }

        return flows.OrderBy(flow => flow).ToList();
    }

    private static IEnumerable<string> InferTopics(FeatureMemorySyncRequest request, Dictionary<string, object?>? memstackData)
    {
        var topics = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var analysisText = BuildAnalysisText(request, memstackData);

        if (ContainsAny(analysisText, "payment", "deposit", "withdraw", "provider", "currency", "bank", "merchant"))
        {
            topics.Add("payment");
        }

        if (ContainsAny(analysisText, "promotion", "bonus", "cashback", "first deposit", "referral", "rebate"))
        {
            topics.Add("promotion");
        }

        var providerName = ExtractProviderName(analysisText);
        if (!string.IsNullOrWhiteSpace(providerName))
        {
            topics.Add(providerName);
        }

        return topics;
    }

    private static string BuildAnalysisText(FeatureMemorySyncRequest request, Dictionary<string, object?>? memstackData)
    {
        return string.Join(
            Environment.NewLine,
            request.Feature.Name ?? string.Empty,
            ExtractString(memstackData, "productName", string.Empty, string.Empty),
            ExtractString(memstackData, "customerName", string.Empty, string.Empty),
            ExtractString(memstackData, "requirement", string.Empty, string.Empty),
            ExtractString(memstackData, "requirementSummary", string.Empty, string.Empty));
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        return needles.Any(needle => text.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractProviderName(string text)
    {
        var knownProviders = new[]
        {
            "paygrid",
            "toppay",
            "aba pay",
            "abapay",
            "sudalink",
            "stripe",
            "telegram pay",
        };

        foreach (var provider in knownProviders)
        {
            if (text.Contains(provider, StringComparison.OrdinalIgnoreCase))
            {
                return provider
                    .ToLowerInvariant()
                    .Replace(" ", "-");
            }
        }

        var lines = text.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (!line.Contains("payment name", StringComparison.OrdinalIgnoreCase)) continue;

            var parts = line.Split(':', 2);
            if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
            {
                return parts[1].Trim().ToLowerInvariant().Replace(" ", "-");
            }
        }

        return string.Empty;
    }

    private static DateTime ParseDateOrFallback(string? value, DateTime fallback)
    {
        return DateTime.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int ScoreFeatureMemory(
        FeatureMemory item,
        string question,
        IReadOnlyList<string> projects,
        IReadOnlyList<string> tags)
    {
        var score = 0;
        var content = string.Join(
            '\n',
            item.Title,
            item.ExternalFeatureId,
            item.ProductName,
            item.CustomerName,
            item.RequirementMarkdown,
            item.ImplementationMarkdown,
            item.SummaryMarkdown,
            item.Tags);

        var loweredContent = content.ToLowerInvariant();
        foreach (var token in ExtractTokens(question))
        {
            if (loweredContent.Contains(token))
            {
                score += 3;
            }
        }

        foreach (var project in projects)
        {
            if (loweredContent.Contains(project.ToLowerInvariant()))
            {
                score += 5;
            }
        }

        foreach (var tag in tags)
        {
            if (item.Tags.Contains(tag, StringComparison.OrdinalIgnoreCase))
            {
                score += 4;
            }
        }

        if (!string.IsNullOrWhiteSpace(item.SummaryMarkdown))
        {
            score += 1;
        }

        return score;
    }

    private static List<string> ExtractTokens(string text)
    {
        return text
            .Split([' ', ',', '.', ':', ';', '!', '?', '\n', '\r', '\t', '-', '_', '/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim().ToLowerInvariant())
            .Where(token => token.Length >= 4)
            .Distinct()
            .ToList();
    }

    private static string BuildAnswer(FeatureMemory item)
    {
        var summary = FirstNonEmptyParagraph(item.SummaryMarkdown);
        if (!string.IsNullOrWhiteSpace(summary))
        {
            return summary;
        }

        var implementation = FirstNonEmptyParagraph(item.ImplementationMarkdown);
        if (!string.IsNullOrWhiteSpace(implementation))
        {
            return implementation;
        }

        var requirement = FirstNonEmptyParagraph(item.RequirementMarkdown);
        if (!string.IsNullOrWhiteSpace(requirement))
        {
            return requirement;
        }

        return $"I found a related feature memory record: {item.Title}.";
    }

    private static List<string> BuildWhy(FeatureMemory item, IReadOnlyList<string> projects)
    {
        var reasons = new List<string>();

        if (!string.IsNullOrWhiteSpace(item.RequirementMarkdown))
        {
            reasons.Add("A matching requirement record exists for this feature.");
        }

        if (!string.IsNullOrWhiteSpace(item.ImplementationMarkdown))
        {
            reasons.Add("Implementation notes are available for the matched feature.");
        }

        if (projects.Count > 0)
        {
            reasons.Add($"Project filter matched: {string.Join(", ", projects)}.");
        }

        reasons.Add($"Most relevant feature: {item.Title}.");
        return reasons;
    }

    private static List<string> BuildPossibleChecks(FeatureMemory item, IReadOnlyList<string> projects, string question)
    {
        var checks = new List<string>();

        if (projects.Count > 0)
        {
            checks.Add($"Verify the current behavior in: {string.Join(", ", projects)}.");
        }

        if (question.Contains("promotion", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("discount", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("price", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("money", StringComparison.OrdinalIgnoreCase))
        {
            checks.Add("Review the stored calculation and business-rule notes for money-related logic.");
        }

        checks.Add($"Compare the current behavior against feature memory record '{item.Title}'.");
        return checks;
    }

    private static List<FeatureMemoryAskSource> BuildSources(IReadOnlyList<FeatureMemory> items, string question, IReadOnlyList<string> projects)
    {
        var sources = new List<FeatureMemoryAskSource>();

        foreach (var item in items)
        {
            if (!string.IsNullOrWhiteSpace(item.SummaryMarkdown))
            {
                sources.Add(CreateSource(item, "Summary"));
            }

            if (!string.IsNullOrWhiteSpace(item.ImplementationMarkdown) &&
                (projects.Count > 0 || question.Contains("how", StringComparison.OrdinalIgnoreCase) || question.Contains("why", StringComparison.OrdinalIgnoreCase)))
            {
                sources.Add(CreateSource(item, "Implementation"));
            }

            if (!string.IsNullOrWhiteSpace(item.RequirementMarkdown))
            {
                sources.Add(CreateSource(item, "Requirement"));
            }
        }

        return sources
            .GroupBy(source => new { source.FeatureMemoryId, source.Section })
            .Select(group => group.First())
            .Take(6)
            .ToList();
    }

    private static FeatureMemoryAskSource CreateSource(FeatureMemory item, string section)
    {
        return new FeatureMemoryAskSource
        {
            FeatureMemoryId = item.Id,
            FeatureTitle = item.Title,
            FeatureExternalId = item.ExternalFeatureId,
            Section = section
        };
    }

    private static string DeriveVerdict(FeatureMemory item, string question)
    {
        if (question.Contains("bug", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("wrong", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("not get", StringComparison.OrdinalIgnoreCase) ||
            question.Contains("did not get", StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(item.ImplementationMarkdown) ? "uncertain" : "expected";
        }

        return "uncertain";
    }

    private static string DeriveConfidence(int topScore, int matchCount)
    {
        if (topScore >= 12 && matchCount > 0)
        {
            return "high";
        }

        if (topScore >= 6)
        {
            return "medium";
        }

        return "low";
    }

    private static string FirstNonEmptyParagraph(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return string.Empty;
        }

        return markdown
            .Split(["\r\n\r\n", "\n\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(section => section.Trim())
            .FirstOrDefault(section => !string.IsNullOrWhiteSpace(section)) ?? string.Empty;
    }
}
