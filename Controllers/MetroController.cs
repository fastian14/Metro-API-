using MetroAPI.Models.Common;
using MetroAPI.Models.Requests;
using MetroAPI.Models.Responses;
using MetroAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace MetroAPI.Controllers;

/// <summary>
/// SparsWeb ↔ Metropolitan carrier integration.
///
/// Two-step workflow:
///   1. POST /api/metro/create-order   → returns tracking number + per-piece barcodes
///   2. POST /api/metro/labels         → returns Base64 PDF labels (can be called multiple times for reprints)
///      POST /api/metro/bol            → returns Base64 PDF Bill of Lading
/// </summary>
[ApiController]
[Route("api/metro")]
[Produces("application/json")]
public class MetroController : ControllerBase
{
    private readonly IMetroService _metroService;
    private readonly ILogger<MetroController> _logger;

    public MetroController(IMetroService metroService, ILogger<MetroController> logger)
    {
        _metroService = metroService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/metro/create-order
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 1 – Create a new shipment order with Metropolitan.
    /// </summary>
    /// <remarks>
    /// Submits the shipment order to Metro and returns:
    /// - The **tracking number (PRO)** used as the key for GetOrderLabels and GetOrderBOL.
    /// - **Per-piece barcodes** and barcode images (Base64 PNG) that SparsWeb should persist.
    /// - Full piece details (weight, freight class, dimensions) as accepted by Metro.
    ///
    /// Store the tracking number and all piece details in SparsWeb — you will need
    /// the tracking number to call GetOrderLabels and GetOrderBOL later.
    /// </remarks>
    /// <param name="request">Shipment details including shipper, consignee, and piece list.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Order created successfully. Returns tracking number and piece barcodes.</response>
    /// <response code="400">Validation failed (missing required fields).</response>
    /// <response code="502">Metro API returned an error.</response>
    [HttpPost("create-order")]
    [ProducesResponseType(typeof(ApiResponse<CreateOrderResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> CreateOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        try
        {
            var result = await _metroService.CreateOrderAsync(request, ct);

            _logger.LogInformation(
                "CreateOrder succeeded → TrackingNumber={Tracking} Pieces={Pieces}",
                result.TrackingNumber, result.TotalPieces);

            return Ok(ApiResponse<CreateOrderResponse>.Ok(result));
        }
        catch (MetroApiException ex)
        {
            _logger.LogWarning(
                "CreateOrder Metro error → Code={Code} Message={Message}",
                ex.ErrorCode, ex.Message);

            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/metro/labels
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 2a – Retrieve shipping labels for an existing order.
    /// </summary>
    /// <remarks>
    /// Returns shipping labels as a **Base64-encoded PDF** (one combined document
    /// plus individual per-piece labels).
    ///
    /// This endpoint can be called multiple times with the same tracking number,
    /// making it ideal for label reprints without recreating the order.
    ///
    /// **Decoding the PDF:**
    /// ```csharp
    /// byte[] pdfBytes = Convert.FromBase64String(response.Data.CombinedLabelsPdfBase64);
    /// File.WriteAllBytes("labels.pdf", pdfBytes);
    /// ```
    /// </remarks>
    /// <param name="request">Tracking number and optional label format/size preferences.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Labels returned as Base64-encoded PDF.</response>
    /// <response code="400">Validation failed (missing tracking number).</response>
    /// <response code="404">Order not found in Metro's system.</response>
    /// <response code="502">Metro API returned an error.</response>
    [HttpPost("labels")]
    [ProducesResponseType(typeof(ApiResponse<GetOrderLabelsResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOrderLabels(
        [FromBody] GetOrderLabelsRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        try
        {
            var result = await _metroService.GetOrderLabelsAsync(request, ct);

            _logger.LogInformation(
                "GetOrderLabels succeeded → TrackingNumber={Tracking} Pieces={Pieces}",
                result.TrackingNumber, result.PieceLabels.Count);

            return Ok(ApiResponse<GetOrderLabelsResponse>.Ok(result));
        }
        catch (MetroApiException ex) when (ex.ErrorCode == "NOT_FOUND")
        {
            return NotFound(ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
        catch (MetroApiException ex)
        {
            _logger.LogWarning(
                "GetOrderLabels Metro error → Code={Code} Message={Message}",
                ex.ErrorCode, ex.Message);

            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST /api/metro/bol
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Step 2b – Retrieve the Bill of Lading for an existing order.
    /// </summary>
    /// <remarks>
    /// Returns the Bill of Lading as a **Base64-encoded PDF** using the tracking
    /// number returned by CreateOrder.
    ///
    /// Like GetOrderLabels, this endpoint is idempotent and safe to call multiple
    /// times for reprints.
    ///
    /// **Decoding the PDF:**
    /// ```csharp
    /// byte[] pdfBytes = Convert.FromBase64String(response.Data.BolPdfBase64);
    /// File.WriteAllBytes("bol.pdf", pdfBytes);
    /// ```
    /// </remarks>
    /// <param name="request">Tracking number and optional copy count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">BOL returned as Base64-encoded PDF.</response>
    /// <response code="400">Validation failed (missing tracking number).</response>
    /// <response code="404">Order not found in Metro's system.</response>
    /// <response code="502">Metro API returned an error.</response>
    [HttpPost("bol")]
    [ProducesResponseType(typeof(ApiResponse<GetOrderBOLResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> GetOrderBOL(
        [FromBody] GetOrderBOLRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(BuildValidationError());

        try
        {
            var result = await _metroService.GetOrderBOLAsync(request, ct);

            _logger.LogInformation(
                "GetOrderBOL succeeded → TrackingNumber={Tracking} BOLNumber={Bol}",
                result.TrackingNumber, result.BolNumber);

            return Ok(ApiResponse<GetOrderBOLResponse>.Ok(result));
        }
        catch (MetroApiException ex) when (ex.ErrorCode == "NOT_FOUND")
        {
            return NotFound(ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
        catch (MetroApiException ex)
        {
            _logger.LogWarning(
                "GetOrderBOL Metro error → Code={Code} Message={Message}",
                ex.ErrorCode, ex.Message);

            return StatusCode(StatusCodes.Status502BadGateway,
                ApiResponse<object>.Fail(ex.ErrorCode, ex.Message));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private ApiResponse<object> BuildValidationError()
    {
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0)
            .SelectMany(x => x.Value!.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}"))
            .ToList();

        return ApiResponse<object>.Fail(
            "VALIDATION_ERROR",
            string.Join(" | ", errors));
    }
}
