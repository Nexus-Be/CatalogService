---
name: controller-conventions
description: Canonical standards for ASP.NET Core thin controllers in CatalogService (port 8002). Covers boundary validation, Catalog.Application shared request/response DTOs, English sentence-case error messages, role verification, and the reference controller blueprint.
---

# Controller Conventions & Standards in CatalogService

> Scope: CatalogService repository only. Never reference parent workspace paths, root documentation, or sibling service directories.

This skill defines the invariant structural and boundary rules for all REST API controllers in `Catalog.Api`.

Controllers serve strictly as **HTTP Transport Boundary Adapters**. They bind incoming HTTP protocol details, invoke Catalog Application use cases, and translate results into standardized HTTP responses. They must never contain business decisions, direct database queries, or manual error formatting.

---

## 1. When to Use

Consult and follow this skill whenever you:
- Create a new controller or add action methods in `Catalog.Api/Controllers/Public/`.
- Review controller code changes for architectural consistency in CatalogService.
- Bind request parameters (`[FromRoute]`, `[FromQuery]`, `[FromBody]`).
- Design validation placement for incoming client payloads.
- Format outbound responses, pagination metadata, or status codes.

---

## 2. Request and Response DTO Placement (Application Layer Sharing)

To prevent duplicate wrapper classes and maintain clean domain boundaries:

- **Declare DTOs in Catalog.Application**: All Request and Response records are declared directly in `Catalog.Application/Services/{Feature}/{Feature}Requests.cs` and `{Feature}Responses.cs`.
- **Direct Consumption by Controller**: Controllers directly bind these Application-layer request records and return these Application-layer response records.
- **Prohibit Duplicate API DTOs**: Never create redundant DTOs in `Catalog.Api` (e.g. prohibited: creating `ApiCreateProductRequest` in Api only to map into `CreateProductRequest` in Application).
- **Domain Entities Stay Private**: Never expose EF Core entities or domain aggregate roots (`Product`, `Category`, `ProductVariant`) through the controller. Only immutable DTO records and `PagedData<T>` leave the application boundary.

---

## 3. Validation Placement (Boundary Discipline)

Validation is concentrated exclusively at system boundaries:

- **Boundary Validation on Request Records**: Place declarative validation attributes from `System.ComponentModel.DataAnnotations` directly on Request DTO records (`[Required]`, `[StringLength]`, `[Range]`, `[RegularExpression]`).
- **Automatic Model State Rejection**: Invalid inputs are intercepted automatically by ASP.NET Core model validation via `ApiValidationProblemFactory`, producing standardized RFC 9110 ProblemDetails with `errorCode = "VAL_INVALID_INPUT"`.
- **PROHIBIT Manual Validation in Controllers**: Never write ad-hoc validation checks inside controller action bodies (e.g. prohibited: `if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest(...);`).

---

## 4. Error Message Standards (Descriptive Sentence Case)

When domain exceptions, validation failures, or edge guards trigger errors:

- **100% English Language**: All error messages, failure details, and exception descriptions must be written in English.
- **Sentence Case Format**:
  - Capitalize ONLY the first letter of the sentence.
  - All subsequent words must be lowercase, except technical acronyms and identifiers (`ID`, `OTP`, `JWT`, `URL`, `UUID`, `Email`, `API`, `HTTP`, `SKU`).
  - Prohibit ALL-CAPS messages (e.g. prohibited: `"PRODUCT NOT FOUND"`, `"CATEGORY NOT FOUND"`).
  - Prohibit Title Case (e.g. prohibited: `"Product Not Found"`).
- **Contextual Precision**: The message must precisely describe what condition failed so any developer or client can diagnose the issue immediately.
  - Correct: `"Product was not found with the specified identifier."`
  - Correct: `"Category name must be unique within the parent level."`
  - Correct: `"Product price must be greater than zero."`
- **Error Codes**: Always `SCREAMING_SNAKE_CASE` without arbitrary `ERR_` prefixes (`PRODUCT_NOT_FOUND`, `CATEGORY_NOT_FOUND`, `VAL_INVALID_INPUT`).

---

## 5. Canonical Reference Controller Blueprint

The following structural skeleton represents the standardized pattern for controllers in `Catalog.Api`:

```csharp
namespace Catalog.Api.Controllers.Public;

using System;
using System.Threading;
using System.Threading.Tasks;
using Catalog.Api.Filters;
using Catalog.Application.Common.Models;
using Catalog.Application.Services.{Feature};
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/v1/{plural-resources}")]
public class {Feature}Controller : ControllerBase
{
    private readonly I{Feature}Service _service;

    public {Feature}Controller(I{Feature}Service service)
    {
        _service = service;
    }

    /// <summary>Lists resources with pagination.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedData<{Feature}Dto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1 || pageSize > 100) pageSize = 20;

        var result = await _service.ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Gets a single resource by its unique identifier.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<{Feature}Dto>> GetById(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Creates a new resource from an Application-layer request record.</summary>
    [HttpPost]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        [FromBody] Create{Feature}Request request,
        CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Updates a resource by its unique identifier.</summary>
    [HttpPut("{id:guid}")]
    [RequireRole("seller", "admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] Update{Feature}Request request,
        CancellationToken ct)
    {
        var result = await _service.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    /// <summary>Deletes a resource by its unique identifier.</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

---

## 6. HTTP Status & Return Conventions

- **GET Single Item**: Return `Task<ActionResult<T>>` with `Ok(dto)` (HTTP 200).
- **GET Paginated List**: Return `Task<ActionResult<PagedData<T>>>` with `Ok(pagedData)` (HTTP 200).
- **POST Creation**: Return `Task<IActionResult>` with `StatusCode(StatusCodes.Status201Created, dto)` (HTTP 201).
- **PUT / PATCH Update**: Return `Task<IActionResult>` with `Ok(dto)` (HTTP 200).
- **DELETE or Empty Operations**: Return `Task<IActionResult>` with `NoContent()` (HTTP 204, 0-byte payload).
- **No Manual Outer Envelopes**: Microservice controllers return pure payloads. Wrapping into `ApiResponse<T>` is performed by Gateway `ResponseEnvelopeMiddleware`.
- **Automatic Error Responses**: All unhandled domain exceptions bubble up to `GlobalExceptionHandlerMiddleware` to format RFC 9110 ProblemDetails.
