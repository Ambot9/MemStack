using LibGit2Sharp;
using MemStack.Model;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace MemStack.Data;

public class GitRepository : IGitRepository
{
    private readonly string _repoPath;
    private readonly ILogger<GitRepository> _logger;
    private readonly string _authorName;
    private readonly string _authorEmail;
    private readonly HttpClient _httpClient = new();
    private static readonly string[] WikiSections =
    [
        "Current Logic",
        "Requirement History",
        "Money Logic",
        "Customer Reply Guide",
        "Related Sprint Items",
        "Related Features",
        "Change Notes"
    ];

    public bool IsEnabled { get; }

    public GitRepository(IConfiguration config, ILogger<GitRepository> logger)
    {
        _logger = logger;
        _repoPath = config["GitPersistence:RepositoryPath"] ?? "./features-repo";
        _authorName = config["GitPersistence:AuthorName"] ?? "MemStack";
        _authorEmail = config["GitPersistence:AuthorEmail"] ?? "bot@memstack.local";
        IsEnabled = bool.TryParse(config["GitPersistence:Enabled"], out var enabled) && enabled;
    }

    public void Initialize()
    {
        if (!IsEnabled) return;
        try
        {
            if (Directory.Exists(Path.Combine(_repoPath, ".git")))
            {
                _logger.LogInformation("Git repo already exists at {path}", _repoPath);
                return;
            }
            _logger.LogWarning(
                "Git persistence is enabled but no existing git repository was found at {path}. Skipping git writes until a valid repo path is configured.",
                _repoPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize git repo");
        }
    }

    public void CommitFeatureMemory(
        FeatureMemory memory,
        string operation = "upsert",
        StorageTargetPayload? storageTarget = null,
        GitAccountPayload? gitAccount = null)
    {
        try
        {
            if (storageTarget is not null && gitAccount is not null &&
                !string.IsNullOrWhiteSpace(storageTarget.Repository) &&
                !string.IsNullOrWhiteSpace(gitAccount.Token))
            {
                PushToRemoteRepository(memory, operation, storageTarget, gitAccount).GetAwaiter().GetResult();
            }

            if (!IsEnabled) return;

            if (!Directory.Exists(Path.Combine(_repoPath, ".git")))
            {
                _logger.LogWarning("Skipping git commit because repository path is not a valid git repo: {path}", _repoPath);
                return;
            }

            using var repo = new Repository(_repoPath);
            var relativePaths = WriteFeatureDocuments(memory);
            relativePaths.AddRange(WriteTopicWikiDocuments(memory));
            relativePaths.Add(WriteProjectContextDocument());

            foreach (var relativePath in relativePaths.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Commands.Stage(repo, relativePath);
            }

            var sig = new Signature(_authorName, _authorEmail, DateTimeOffset.UtcNow);
            repo.Commit($"{operation.ToUpperInvariant()}: {GetFeatureFolderRelativePath(memory)}", sig, sig);
            _logger.LogInformation("Git commit [{op}]: {path}", operation, GetFeatureFolderRelativePath(memory));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git commit failed for {id}", memory.ExternalFeatureId);
            throw;
        }
    }

    private async Task PushToRemoteRepository(
        FeatureMemory memory,
        string operation,
        StorageTargetPayload storageTarget,
        GitAccountPayload gitAccount)
    {
        _logger.LogInformation("[MemStack] Starting PushToRemoteRepository for feature {id} with operation {operation} to repo {repo} (provider: {provider})", memory.ExternalFeatureId, operation, storageTarget.Repository, gitAccount.Provider);
        var files = BuildRemoteFiles(memory, storageTarget);
        foreach (var file in files)
        {
            _logger.LogInformation("[MemStack] Preparing to upsert remote file: {path}", file.Path);
            await UpsertRemoteFile(file.Path, file.Content, $"{operation.ToUpperInvariant()}: {file.Path}", storageTarget, gitAccount);
            _logger.LogInformation("[MemStack] Finished upsert for remote file: {path}", file.Path);
        }
        _logger.LogInformation("[MemStack] Completed PushToRemoteRepository for feature {id}", memory.ExternalFeatureId);
    }

    private List<(string Path, string Content)> BuildRemoteFiles(FeatureMemory memory, StorageTargetPayload storageTarget)
    {
        var featureRoot = (storageTarget.Path ?? "Features").Trim('/').Trim();
        if (string.IsNullOrWhiteSpace(featureRoot))
        {
            featureRoot = "Features";
        }

        var year = memory.UpdatedAtUtc.Year.ToString();
        var slug = ToSlug(memory.ExternalFeatureId);
        return
        [
            ($"{featureRoot}/{year}/{slug}/requirement.md", BuildRequirementMarkdown(memory)),
            ($"{featureRoot}/{year}/{slug}/implementation.md", BuildImplementationMarkdown(memory)),
            ..ExtractTopics(memory).Select(topic => ($"Wiki/{topic}.md", BuildTopicWikiDocument(memory, topic))),
            ("PROJECT_CONTEXT.md", BuildProjectContextMarkdown())
        ];
    }

    private string BuildTopicWikiDocument(FeatureMemory memory, string topic)
    {
        var content = BuildTopicWikiTemplate(topic);
        var featureKey = ToSlug(memory.ExternalFeatureId);
        content = UpsertSectionEntry(content, "Current Logic", featureKey, BuildCurrentLogicEntry(memory));
        content = UpsertSectionEntry(content, "Requirement History", featureKey, BuildRequirementHistoryEntry(memory));
        content = UpsertSectionEntry(content, "Money Logic", featureKey, BuildMoneyLogicEntry(memory));
        content = UpsertSectionEntry(content, "Customer Reply Guide", featureKey, BuildCustomerReplyEntry(memory));
        content = UpsertSectionEntry(content, "Related Sprint Items", featureKey, BuildSprintEntry(memory));
        content = UpsertSectionEntry(content, "Related Features", featureKey, BuildRelatedFeatureEntry(memory));
        content = UpsertSectionEntry(content, "Change Notes", featureKey, BuildChangeNotesEntry(memory));
        return content;
    }

    private async Task UpsertRemoteFile(
        string relativePath,
        string content,
        string commitMessage,
        StorageTargetPayload storageTarget,
        GitAccountPayload gitAccount)
    {
        _logger.LogInformation("[MemStack] UpsertRemoteFile called for {provider} {repo} {branch} path={path}", gitAccount.Provider, storageTarget.Repository, storageTarget.Branch, relativePath);
        if (gitAccount.Provider == "github")
        {
            await UpsertGithubFile(relativePath, content, commitMessage, storageTarget, gitAccount);
            _logger.LogInformation("[MemStack] UpsertGithubFile completed for {path}", relativePath);
            return;
        }

        if (gitAccount.Provider == "gitlab" || gitAccount.Provider == "gitlab-self-hosted")
        {
            await UpsertGitlabFile(relativePath, content, commitMessage, storageTarget, gitAccount);
            _logger.LogInformation("[MemStack] UpsertGitlabFile completed for {path}", relativePath);
            return;
        }

        _logger.LogWarning("Unsupported remote provider for MemStack sync: {provider}", gitAccount.Provider);
    }

    private async Task UpsertGithubFile(
        string relativePath,
        string content,
        string commitMessage,
        StorageTargetPayload storageTarget,
        GitAccountPayload gitAccount)
    {
        var requestUri = $"https://api.github.com/repos/{storageTarget.Repository}/contents/{relativePath}?ref={storageTarget.Branch}";
        string? sha = null;

        using (var getRequest = new HttpRequestMessage(HttpMethod.Get, requestUri))
        {
            getRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", gitAccount.Token);
            getRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
            getRequest.Headers.UserAgent.ParseAdd("MemStack");

            using var getResponse = await _httpClient.SendAsync(getRequest);
            if (getResponse.IsSuccessStatusCode)
            {
                var existing = await getResponse.Content.ReadFromJsonAsync<GithubContentResponse>();
                sha = existing?.sha;
            }
            else if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException($"GitHub file lookup failed for {relativePath}: {(int)getResponse.StatusCode}");
            }
        }

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, $"https://api.github.com/repos/{storageTarget.Repository}/contents/{relativePath}");
        putRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", gitAccount.Token);
        putRequest.Headers.Accept.ParseAdd("application/vnd.github+json");
        putRequest.Headers.UserAgent.ParseAdd("MemStack");
        putRequest.Content = JsonContent.Create(new
        {
            message = commitMessage,
            content = Convert.ToBase64String(Encoding.UTF8.GetBytes(content)),
            branch = storageTarget.Branch,
            sha
        });

        using var putResponse = await _httpClient.SendAsync(putRequest);
        if (!putResponse.IsSuccessStatusCode)
        {
            var body = await putResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitHub file upsert failed for {relativePath}: {body}");
        }
    }

    private async Task UpsertGitlabFile(
        string relativePath,
        string content,
        string commitMessage,
        StorageTargetPayload storageTarget,
        GitAccountPayload gitAccount)
    {
        var baseUrl = string.IsNullOrWhiteSpace(gitAccount.GitlabUrl) ? "https://gitlab.com" : gitAccount.GitlabUrl.TrimEnd('/');
        var projectId = Uri.EscapeDataString(storageTarget.Repository);
        var filePath = Uri.EscapeDataString(relativePath);
        var requestUri = $"{baseUrl}/api/v4/projects/{projectId}/repository/files/{filePath}?ref={Uri.EscapeDataString(storageTarget.Branch)}";

        var exists = false;
        using (var getRequest = new HttpRequestMessage(HttpMethod.Get, requestUri))
        {
            getRequest.Headers.Add("PRIVATE-TOKEN", gitAccount.Token);
            using var getResponse = await _httpClient.SendAsync(getRequest);
            exists = getResponse.IsSuccessStatusCode;
            if (!exists && getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                var body = await getResponse.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"GitLab file lookup failed for {relativePath}: {body}");
            }
        }

        var endpoint = $"{baseUrl}/api/v4/projects/{projectId}/repository/files/{filePath}";
        var bodyData = new Dictionary<string, string>
        {
            ["branch"] = storageTarget.Branch,
            ["content"] = content,
            ["commit_message"] = commitMessage,
        };

        using var writeRequest = new HttpRequestMessage(exists ? HttpMethod.Put : HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(bodyData)
        };
        writeRequest.Headers.Add("PRIVATE-TOKEN", gitAccount.Token);

        using var writeResponse = await _httpClient.SendAsync(writeRequest);
        if (!writeResponse.IsSuccessStatusCode)
        {
            var body = await writeResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"GitLab file upsert failed for {relativePath}: {body}");
        }
    }

    public void DeleteFeatureMemory(FeatureMemory memory)
    {
        if (!IsEnabled) return;
        try
        {
            if (!Directory.Exists(Path.Combine(_repoPath, ".git")))
            {
                _logger.LogWarning("Skipping git delete because repository path is not a valid git repo: {path}", _repoPath);
                return;
            }

            using var repo = new Repository(_repoPath);
            var featureFolder = GetFeatureFolderPath(memory);
            if (!Directory.Exists(featureFolder)) return;

            foreach (var filePath in Directory.GetFiles(featureFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(_repoPath, filePath).Replace('\\', '/');
                Commands.Remove(repo, relativePath);
            }

            if (Directory.Exists(featureFolder))
            {
                Directory.Delete(featureFolder, true);
            }

            var sig = new Signature(_authorName, _authorEmail, DateTimeOffset.UtcNow);
            repo.Commit($"DELETE: {GetFeatureFolderRelativePath(memory)}", sig, sig);
            _logger.LogInformation("Git delete: {path}", GetFeatureFolderRelativePath(memory));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Git delete failed for {id}", memory.ExternalFeatureId);
        }
    }

    private List<string> WriteFeatureDocuments(FeatureMemory memory)
    {
        var featureFolder = GetFeatureFolderPath(memory);
        Directory.CreateDirectory(featureFolder);

        var requirementPath = Path.Combine(featureFolder, "requirement.md");
        var implementationPath = Path.Combine(featureFolder, "implementation.md");

        File.WriteAllText(requirementPath, BuildRequirementMarkdown(memory));
        File.WriteAllText(implementationPath, BuildImplementationMarkdown(memory));

        return
        [
            Path.GetRelativePath(_repoPath, requirementPath).Replace('\\', '/'),
            Path.GetRelativePath(_repoPath, implementationPath).Replace('\\', '/')
        ];
    }

    private List<string> WriteTopicWikiDocuments(FeatureMemory memory)
    {
        var writtenPaths = new List<string>();
        var wikiFolder = Path.Combine(_repoPath, "Wiki");
        Directory.CreateDirectory(wikiFolder);

        foreach (var topic in ExtractTopics(memory))
        {
            var wikiPath = Path.Combine(wikiFolder, $"{topic}.md");
            var content = File.Exists(wikiPath) ? File.ReadAllText(wikiPath) : BuildTopicWikiTemplate(topic);

            var featureKey = ToSlug(memory.ExternalFeatureId);
            content = UpsertSectionEntry(content, "Current Logic", featureKey, BuildCurrentLogicEntry(memory));
            content = UpsertSectionEntry(content, "Requirement History", featureKey, BuildRequirementHistoryEntry(memory));
            content = UpsertSectionEntry(content, "Money Logic", featureKey, BuildMoneyLogicEntry(memory));
            content = UpsertSectionEntry(content, "Customer Reply Guide", featureKey, BuildCustomerReplyEntry(memory));
            content = UpsertSectionEntry(content, "Related Sprint Items", featureKey, BuildSprintEntry(memory));
            content = UpsertSectionEntry(content, "Related Features", featureKey, BuildRelatedFeatureEntry(memory));
            content = UpsertSectionEntry(content, "Change Notes", featureKey, BuildChangeNotesEntry(memory));

            File.WriteAllText(wikiPath, content);
            writtenPaths.Add(Path.GetRelativePath(_repoPath, wikiPath).Replace('\\', '/'));
        }

        return writtenPaths;
    }

    private string WriteProjectContextDocument()
    {
        var path = Path.Combine(_repoPath, "PROJECT_CONTEXT.md");
        File.WriteAllText(path, BuildProjectContextMarkdown());
        return "PROJECT_CONTEXT.md";
    }

    private static string BuildRequirementMarkdown(FeatureMemory m) => $"""
# Feature Requirement

## Feature Info
- Feature ID: {m.ExternalFeatureId}
- Title: {m.Title}
- Source System: {m.SourceSystem}
- Product: {m.ProductName}
- Customer: {m.CustomerName}
- Status: {m.Status}
- Tags: {FormatInlineList(m.Tags)}
- Updated: {m.UpdatedAtUtc:yyyy-MM-dd HH:mm} UTC

## Raw Requirement
{EnsureSectionContent(m.RequirementMarkdown)}

## Requirement Summary
{EnsureSectionContent(FirstNonEmpty(m.SummaryMarkdown, m.RequirementMarkdown))}

## Related Projects
- Not recorded yet in the current API payload.
- Use tags and product context until explicit project storage is added.

## Business Rules
{EnsureSectionContent(ExtractBusinessRules(m))}

## Acceptance Criteria
{EnsureSectionContent(BuildAcceptanceCriteria(m))}

## Related Sprint Items
{EnsureSectionContent(ExtractSprintReferences(m))}
""";

    private static string BuildImplementationMarkdown(FeatureMemory m) => $"""
# Feature Implementation

## Implementation Summary
{EnsureSectionContent(FirstNonEmpty(m.ImplementationMarkdown, m.SummaryMarkdown))}

## Changed Projects
- Not recorded yet in the current API payload.
- Infer related systems from tags, product name, and implementation notes.

## Logic Changed
{EnsureSectionContent(ExtractLogicChanged(m))}

## Money Logic
{EnsureSectionContent(ExtractMoneyLogic(m))}

## Cross-Project Relationship
{EnsureSectionContent(BuildCrossProjectRelationship(m))}

## Known Limitations
{EnsureSectionContent(BuildKnownLimitations(m))}

## Update History
### {m.UpdatedAtUtc:yyyy-MM-dd}
- Status: {m.Status}
- Source: {m.SourceSystem}
- Summary: {FirstNonEmpty(m.SummaryMarkdown, m.ImplementationMarkdown, m.RequirementMarkdown)}
""";

    private static string BuildTopicWikiTemplate(string topic)
    {
        var title = string.Join(' ', topic.Split('-', StringSplitOptions.RemoveEmptyEntries).Select(Capitalize));
        var builder = new StringBuilder();
        builder.AppendLine($"# Topic: {title}");
        builder.AppendLine();
        builder.AppendLine("This wiki page accumulates business knowledge across multiple feature records.");
        builder.AppendLine();

        foreach (var section in WikiSections)
        {
            builder.AppendLine($"## {section}");
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd() + Environment.NewLine;
    }

    private static string BuildCurrentLogicEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        FirstNonEmpty(memory.SummaryMarkdown, memory.ImplementationMarkdown, memory.RequirementMarkdown));

    private static string BuildRequirementHistoryEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        FirstNonEmpty(memory.RequirementMarkdown, memory.SummaryMarkdown));

    private static string BuildMoneyLogicEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        ExtractMoneyLogic(memory));

    private static string BuildCustomerReplyEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        $"Explain the behavior using the feature summary and confirm whether the user's case matches the stored rules. {FirstNonEmpty(memory.SummaryMarkdown, memory.RequirementMarkdown)}");

    private static string BuildSprintEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        ExtractSprintReferences(memory));

    private static string BuildRelatedFeatureEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        $"- Feature Folder: {GetFeatureFolderRelativePath(memory)}{Environment.NewLine}- Status: {memory.Status}{Environment.NewLine}- Tags: {FormatInlineList(memory.Tags)}");

    private static string BuildChangeNotesEntry(FeatureMemory memory) => BuildWikiEntry(
        memory,
        FirstNonEmpty(memory.ImplementationMarkdown, memory.SummaryMarkdown, memory.RequirementMarkdown));

    private static string BuildWikiEntry(FeatureMemory memory, string body)
    {
        var content = EnsureSectionContent(body);
        return $"""
### {memory.Title} ({memory.UpdatedAtUtc:yyyy-MM-dd})
- Feature ID: {memory.ExternalFeatureId}
- Product: {memory.ProductName}
- Customer: {memory.CustomerName}
- Status: {memory.Status}
- Tags: {FormatInlineList(memory.Tags)}

{content}
""";
    }

    private static string UpsertSectionEntry(string document, string sectionTitle, string key, string entry)
    {
        var startMarker = $"<!-- memstack:{sectionTitle}:{key}:start -->";
        var endMarker = $"<!-- memstack:{sectionTitle}:{key}:end -->";
        var wrappedEntry = $"{startMarker}{Environment.NewLine}{entry.TrimEnd()}{Environment.NewLine}{endMarker}";

        if (document.Contains(startMarker, StringComparison.Ordinal))
        {
            var startIndex = document.IndexOf(startMarker, StringComparison.Ordinal);
            var endIndex = document.IndexOf(endMarker, StringComparison.Ordinal);
            if (startIndex >= 0 && endIndex >= startIndex)
            {
                endIndex += endMarker.Length;
                return document[..startIndex] + wrappedEntry + document[endIndex..];
            }
        }

        var sectionHeader = $"## {sectionTitle}";
        var nextSectionIndex = document.Length;
        var sectionIndex = document.IndexOf(sectionHeader, StringComparison.Ordinal);
        if (sectionIndex < 0)
        {
            document = document.TrimEnd() + $"{Environment.NewLine}{Environment.NewLine}{sectionHeader}{Environment.NewLine}";
            sectionIndex = document.IndexOf(sectionHeader, StringComparison.Ordinal);
            nextSectionIndex = document.Length;
        }
        else
        {
            foreach (var candidate in WikiSections.Where(candidate => candidate != sectionTitle))
            {
                var candidateIndex = document.IndexOf($"## {candidate}", sectionIndex + sectionHeader.Length, StringComparison.Ordinal);
                if (candidateIndex > sectionIndex)
                {
                    nextSectionIndex = Math.Min(nextSectionIndex, candidateIndex);
                }
            }
        }

        var insertionPoint = nextSectionIndex;
        var insertionText = $"{Environment.NewLine}{wrappedEntry}{Environment.NewLine}";
        return document.Insert(insertionPoint, insertionText);
    }

    private static string BuildProjectContextMarkdown() => """
# MemStack Project Context

## What Is MemStack
MemStack is a knowledge backend for storing and structuring business feature memory.

## Why MemStack Exists
Teams need a durable record of:
- what customers requested
- what was implemented
- how business logic currently works
- how money-related logic is handled
- how to answer customer questions later

## Relationship With Nexwork
Nexwork is the workflow tool.
MemStack is the knowledge store.
Nexwork sends requirement and implementation context to MemStack, and MemStack writes structured markdown plus searchable metadata.

## Core Document Types
- `Features/<year>/<feature-slug>/requirement.md`
- `Features/<year>/<feature-slug>/implementation.md`
- `Wiki/<topic>.md`

## Why Markdown Is Used
Markdown is readable by humans, easy to version in Git, and easy for AI retrieval when sectioned correctly.

## Retrieval Guidance
AI should prefer:
1. metadata filters
2. section-level retrieval
3. small grounded answers with sources

## Long-Term Goal
MemStack should help answer future questions like:
- what was requested?
- what changed?
- how does this business rule work now?
- is this likely expected behavior or a bug?
""";

    private static IEnumerable<string> ExtractTopics(FeatureMemory memory)
    {
        var topics = memory.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ToSlug)
            .Where(topic => !string.IsNullOrWhiteSpace(topic))
            .ToList();

        if (topics.Count == 0 && !string.IsNullOrWhiteSpace(memory.ProductName))
        {
            topics.Add(ToSlug(memory.ProductName));
        }

        if (topics.Count == 0)
        {
            topics.Add("general");
        }

        return topics.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static string ExtractBusinessRules(FeatureMemory memory)
    {
        var lines = ExtractRelevantLines(memory.RequirementMarkdown, ["rule", "when", "if", "must", "should", "promotion", "discount"]);
        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines.Select(line => $"- {line}"))
            : "- No explicit business rules were extracted yet.";
    }

    private static string BuildAcceptanceCriteria(FeatureMemory memory)
    {
        var lines = ExtractRelevantLines(memory.RequirementMarkdown, ["accept", "criteria", "must", "should", "expected"]);
        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines.Select(line => $"- {line}"))
            : "- Acceptance criteria have not been structured yet.";
    }

    private static string ExtractLogicChanged(FeatureMemory memory)
    {
        var lines = ExtractRelevantLines(FirstNonEmpty(memory.ImplementationMarkdown, memory.SummaryMarkdown), ["logic", "handle", "process", "calculate", "apply", "validate"]);
        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines.Select(line => $"- {line}"))
            : FirstNonEmpty(memory.ImplementationMarkdown, memory.SummaryMarkdown, "No implementation logic summary recorded yet.");
    }

    private static string ExtractMoneyLogic(FeatureMemory memory)
    {
        var lines = ExtractRelevantLines(
            string.Join(Environment.NewLine, memory.RequirementMarkdown, memory.ImplementationMarkdown, memory.SummaryMarkdown),
            ["money", "price", "pricing", "amount", "promotion", "discount", "tax", "fee", "payment", "total", "round"]);

        return lines.Count > 0
            ? string.Join(Environment.NewLine, lines.Select(line => $"- {line}"))
            : "- No explicit money-related logic was extracted from this feature record.";
    }

    private static string BuildCrossProjectRelationship(FeatureMemory memory)
    {
        if (!string.IsNullOrWhiteSpace(memory.Tags))
        {
            return $"- Related domains inferred from tags: {FormatInlineList(memory.Tags)}{Environment.NewLine}- Add explicit project storage in a later schema update for stronger cross-project mapping.";
        }

        return "- Cross-project relationship data is not recorded yet in the current API payload.";
    }

    private static string BuildKnownLimitations(FeatureMemory memory)
    {
        var notes = new List<string>
        {
            "Structured project relationships are not stored yet in the database."
        };

        if (string.IsNullOrWhiteSpace(memory.ImplementationMarkdown))
        {
            notes.Add("Implementation markdown is still empty for this feature.");
        }

        return string.Join(Environment.NewLine, notes.Select(note => $"- {note}"));
    }

    private static string ExtractSprintReferences(FeatureMemory memory)
    {
        var sprintTags = memory.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => tag.Contains("sprint", StringComparison.OrdinalIgnoreCase) || tag.Contains("iteration", StringComparison.OrdinalIgnoreCase))
            .ToList();

        return sprintTags.Count > 0
            ? string.Join(Environment.NewLine, sprintTags.Select(tag => $"- {tag}"))
            : "- No sprint metadata recorded.";
    }

    private static List<string> ExtractRelevantLines(string text, string[] keywords)
    {
        return text
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim().TrimStart('-', '*', '#', ' '))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Where(line => keywords.Any(keyword => line.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();
    }

    private static string EnsureSectionContent(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "- Not recorded yet." : value.Trim();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }

    private static string FormatInlineList(string? csv)
    {
        var items = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return items.Count > 0 ? string.Join(", ", items) : "none";
    }

    private string GetFeatureFolderPath(FeatureMemory memory)
    {
        return Path.Combine(_repoPath, GetFeatureFolderRelativePath(memory).Replace('/', Path.DirectorySeparatorChar));
    }

    private static string GetFeatureFolderRelativePath(FeatureMemory memory)
    {
        var year = memory.UpdatedAtUtc.Year.ToString();
        var slug = ToSlug(memory.ExternalFeatureId);
        return $"Features/{year}/{slug}";
    }

    // "FEAT-1001" -> "feat-1001", "feature/engine-v2" -> "feature-engine-v2"
    private static string ToSlug(string value) =>
        value.ToLowerInvariant()
            .Replace(" ", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Trim('-');

    private static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToUpperInvariant(value[0]) + value[1..];
    }
}

internal sealed class GithubContentResponse
{
    public string? sha { get; set; }
}
