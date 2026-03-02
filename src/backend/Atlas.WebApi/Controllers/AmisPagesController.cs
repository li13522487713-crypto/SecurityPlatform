using Atlas.Application.Amis.Abstractions;
using Atlas.Application.Amis.Models;
using Atlas.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace Atlas.WebApi.Controllers;

[ApiController]
[Route("api/v1/amis/pages")]
public sealed class AmisPagesController : ControllerBase
{
    private static readonly Regex KeyPattern = new(
        "^[a-zA-Z0-9_-]{1,64}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IAmisSchemaProvider _schemaProvider;

    public AmisPagesController(IAmisSchemaProvider schemaProvider)
    {
        _schemaProvider = schemaProvider;
    }

    [HttpGet("{key}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<AmisPageDefinition>>> GetByKey(
        string key,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return BadRequest(ApiResponse<AmisPageDefinition>.Fail(
                ErrorCodes.ValidationError,
                "Schema Key 不能为空",
                HttpContext.TraceIdentifier));
        }

        if (!KeyPattern.IsMatch(key))
        {
            return BadRequest(ApiResponse<AmisPageDefinition>.Fail(
                ErrorCodes.ValidationError,
                "Schema Key 格式不合法",
                HttpContext.TraceIdentifier));
        }

        var schema = await _schemaProvider.GetByKeyAsync(key, cancellationToken);
        if (schema is null)
        {
            return NotFound(ApiResponse<AmisPageDefinition>.Fail(
                ErrorCodes.NotFound,
                "Schema 未找到",
                HttpContext.TraceIdentifier));
        }

        return Ok(ApiResponse<AmisPageDefinition>.Ok(schema, HttpContext.TraceIdentifier));
    }
}
