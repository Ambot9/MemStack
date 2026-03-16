using MemStack.Model;
using MemStack.Services;
using Microsoft.AspNetCore.Mvc;

namespace MemStack.Controller;

[ApiController]
[Route("api/feature-memories")]
public class FeatureMemoryController(IFeatureMemoryService service) : ControllerBase
{

    [HttpGet]
    public ActionResult<IReadOnlyList<FeatureMemoryResponse>> List()
    {
        return Ok(service.GetAll());
    }

    [HttpPost("search")]
    public ActionResult<IReadOnlyList<FeatureMemoryResponse>> Search([FromBody] FeatureMemorySearchRequest request)
    {
        return Ok(service.Search(request));
    }

    [HttpPost("ask")]
    public ActionResult<FeatureMemoryAskResponse> Ask([FromBody] FeatureMemoryAskRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            var errors = new Dictionary<string, string[]>
            {
                [nameof(FeatureMemoryAskRequest.Question)] = ["Question is required."]
            };

            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return Ok(service.Ask(request));
    }

    [HttpPost("sync-from-nexwork")]
    public ActionResult<FeatureMemoryResponse> SyncFromNexwork([FromBody] FeatureMemorySyncRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Feature?.Name))
        {
            var errors = new Dictionary<string, string[]>
            {
                [nameof(FeatureMemorySyncRequest.Feature)] = ["Feature payload is required."]
            };
            return ValidationProblem(new ValidationProblemDetails(errors));
        }

        return Ok(service.SyncFromNexwork(request));
    }

    [HttpGet("{id:int}")]
    public ActionResult<FeatureMemoryResponse> GetById(int id)
    {
        var found = service.GetById(id);
        return found is null ? NotFound() : Ok(found);
    }

    [HttpPost]
    public ActionResult<FeatureMemoryResponse> Create([FromBody] FeatureMemoryRequest request)
    {
        var statusError = ValidateStatus(request.Status);
        if (statusError is not null)
        {
            return statusError;
        }

        var created = service.Create(request);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public ActionResult<FeatureMemoryResponse> Update(int id, [FromBody] FeatureMemoryRequest request)
    {
        var statusError = ValidateStatus(request.Status);
        if (statusError is not null) return statusError;

        var updated = service.Update(id, request);
        return updated is null ? NotFound() : Ok(updated);
    }

    // PATCH: partial update — only the fields you send are changed (guide Step 6)
    [HttpPatch("{id:int}")]
    public ActionResult<FeatureMemoryResponse> Patch(int id, [FromBody] FeatureMemoryPatchRequest request)
    {
        if (request.Status is not null)
        {
            var statusError = ValidateStatus(request.Status);
            if (statusError is not null) return statusError;
        }

        var patched = service.Patch(id, request);
        return patched is null ? NotFound() : Ok(patched);
    }

    // Sync summary — called by Nexwork when a feature is completed (guide Step 6 + 7)
    [HttpPost("{id:int}/sync-summary")]
    public ActionResult<FeatureMemoryResponse> SyncSummary(int id, [FromBody] SyncSummaryRequest request)
    {
        var result = service.SyncSummary(id, request.SummaryMarkdown);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        return service.Delete(id) ? NoContent() : NotFound();
    }

    private ActionResult? ValidateStatus(string status)
    {
        if (service.IsValidStatus(status))
        {
            return null;
        }

        var errors = new Dictionary<string, string[]>
        {
            [nameof(FeatureMemoryRequest.Status)] =
            ["Status must be one of: Planned, InProgress, Done, Blocked."]
        };

        return ValidationProblem(new ValidationProblemDetails(errors));
    }
}
