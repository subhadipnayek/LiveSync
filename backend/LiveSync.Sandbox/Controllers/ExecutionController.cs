using LiveSync.Execution.Contracts;
using LiveSync.Sandbox.Services;
using Microsoft.AspNetCore.Mvc;

namespace LiveSync.Sandbox.Controllers;

[ApiController]
[Route("api/execution")]
public sealed class ExecutionController : ControllerBase
{
    private readonly ISandboxExecutionService _sandboxExecutionService;

    public ExecutionController(ISandboxExecutionService sandboxExecutionService)
    {
        _sandboxExecutionService = sandboxExecutionService;
    }

    [HttpGet("languages")]
    [ProducesResponseType(typeof(IReadOnlyList<ExecutionLanguageDescriptor>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<ExecutionLanguageDescriptor>> GetLanguages()
    {
        return Ok(_sandboxExecutionService.GetLanguages());
    }

    [HttpPost("run")]
    [ProducesResponseType(typeof(SandboxExecutionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SandboxExecutionResponse>> Execute([FromBody] SandboxExecutionRequest request, CancellationToken cancellationToken)
    {
        var result = await _sandboxExecutionService.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }
}
