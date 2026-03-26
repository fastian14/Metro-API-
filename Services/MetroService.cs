using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using MetroAPI.Configuration;
using MetroAPI.Models.Requests;
using MetroAPI.Models.Responses;
using Microsoft.Extensions.Options;

namespace MetroAPI.Services;

/// <summary>
/// Concrete implementation that calls the Metropolitan carrier REST API.
///
/// Authentication: Before every request <see cref="IMetroTokenService"/> is asked
/// for a valid bearer token (fetched/refreshed transparently). The token is then
/// set on a per-request <see cref="HttpRequestMessage"/> so that concurrent calls
/// never overwrite each other's Authorization header.
///
/// Error handling: Any non-2xx response from Metro is deserialized into a
/// <see cref="MetroErrorResponse"/> and re-thrown as an <see cref="MetroApiException"/>,
/// which the controller catches and maps to the appropriate HTTP status code.
/// </summary>
public class MetroService : IMetroService
{
    private readonly HttpClient _http;
    private readonly MetroApiSettings _settings;
    private readonly IMetroTokenService _tokenService;
    private readonly ILogger<MetroService> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public MetroService(
        HttpClient http,
        IOptions<MetroApiSettings> settings,
        IMetroTokenService tokenService,
        ILogger<MetroService> logger)
    {
        _http         = http;
        _settings     = settings.Value;
        _tokenService = tokenService;
        _logger       = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // CreateOrder
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<CreateOrderResponse> CreateOrderAsync(
        CreateOrderRequest request,
        CancellationToken ct = default)
    {
        // Fall back to the configured account number when the caller omits it
        if (string.IsNullOrWhiteSpace(request.AccountNumber))
            request.AccountNumber = _settings.AccountNumber;

        _logger.LogInformation(
            "CreateOrder → Account={Account} Reference={Ref} Pieces={Pieces}",
            request.AccountNumber, request.ReferenceNumber, request.Pieces.Count);

        // Map our SparsWeb request model to the Metro API contract
        var metroPayload = BuildCreateOrderPayload(request);

        var response = await PostAsync<MetroCreateOrderResponse>(
            _settings.CreateOrderPath, metroPayload, ct);

        return MapCreateOrderResponse(response);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetOrderLabels
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GetOrderLabelsResponse> GetOrderLabelsAsync(
        GetOrderLabelsRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GetOrderLabels → TrackingNumber={Tracking} Format={Format}",
            request.TrackingNumber, request.LabelFormat);

        var metroPayload = new
        {
            trackingNumber  = request.TrackingNumber,
            pieceBarcodes   = request.PieceBarcodes ?? new List<string>(),
            labelFormat     = request.LabelFormat,
            labelSize       = request.LabelSize
        };

        var response = await PostAsync<MetroGetLabelsResponse>(
            _settings.GetOrderLabelsPath, metroPayload, ct);

        return MapGetLabelsResponse(response, request);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GetOrderBOL
    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<GetOrderBOLResponse> GetOrderBOLAsync(
        GetOrderBOLRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "GetOrderBOL → TrackingNumber={Tracking} Copies={Copies}",
            request.TrackingNumber, request.Copies);

        var metroPayload = new
        {
            trackingNumber = request.TrackingNumber,
            copies         = request.Copies
        };

        var response = await PostAsync<MetroGetBolResponse>(
            _settings.GetOrderBOLPath, metroPayload, ct);

        return MapGetBolResponse(response);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Serializes <paramref name="payload"/> as JSON, POSTs to <paramref name="path"/>
    /// with a fresh bearer token, reads the Metro JSON response, and deserializes it to
    /// <typeparamref name="T"/>. Throws <see cref="MetroApiException"/> on any non-2xx status.
    ///
    /// Token is attached per-<see cref="HttpRequestMessage"/> (not on the shared
    /// <see cref="HttpClient"/> default headers) to keep concurrent requests safe.
    /// </summary>
    private async Task<T> PostAsync<T>(string path, object payload, CancellationToken ct)
    {
        // Obtain a valid bearer token (cached by MetroTokenService)
        var token = await _tokenService.GetAccessTokenAsync(ct);

        var json    = JsonSerializer.Serialize(payload, _jsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        // Build an explicit HttpRequestMessage so we can set per-request auth header
        using var requestMessage = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = content
        };
        requestMessage.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _http.SendAsync(requestMessage, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error calling Metro API path={Path}", path);
            throw new MetroApiException("NETWORK_ERROR",
                $"Could not reach the Metro API: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Metro API request timed out path={Path}", path);
            throw new MetroApiException("TIMEOUT",
                "The Metro API did not respond within the configured timeout.");
        }

        var responseBody = await httpResponse.Content.ReadAsStringAsync(ct);

        if (!httpResponse.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "Metro API returned {Status} for path={Path} body={Body}",
                (int)httpResponse.StatusCode, path, responseBody);

            // Try to extract Metro error details
            MetroErrorResponse? errorDetail = null;
            try
            {
                errorDetail = JsonSerializer.Deserialize<MetroErrorResponse>(
                    responseBody, _jsonOptions);
            }
            catch { /* ignore parse failures on error bodies */ }

            throw new MetroApiException(
                errorDetail?.ErrorCode ?? httpResponse.StatusCode.ToString(),
                errorDetail?.ErrorMessage ?? $"Metro API error: HTTP {(int)httpResponse.StatusCode}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(responseBody, _jsonOptions)
                   ?? throw new MetroApiException("EMPTY_RESPONSE",
                       "Metro API returned an empty or null response body.");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize Metro API response path={Path}", path);
            throw new MetroApiException("DESERIALIZATION_ERROR",
                "Could not parse the Metro API response.");
        }
    }

    // ── Payload builders ──────────────────────────────────────────────────────

    private static object BuildCreateOrderPayload(CreateOrderRequest r) => new
    {
        accountNumber   = r.AccountNumber,
        serviceType     = r.ServiceType,
        referenceNumber = r.ReferenceNumber,
        purchaseOrderNumber = r.PurchaseOrderNumber,
        specialInstructions = r.SpecialInstructions,
        pickupDate      = r.PickupDate?.ToString("yyyy-MM-dd"),
        isResidential   = r.IsResidential,
        liftGateRequired = r.LiftGateRequired,
        insideDelivery  = r.InsideDelivery,
        shipper = new
        {
            name         = r.Shipper.Name,
            address1     = r.Shipper.Address1,
            address2     = r.Shipper.Address2,
            city         = r.Shipper.City,
            state        = r.Shipper.State,
            zip          = r.Shipper.Zip,
            country      = r.Shipper.Country,
            phone        = r.Shipper.Phone,
            contactName  = r.Shipper.ContactName
        },
        consignee = new
        {
            name         = r.Consignee.Name,
            address1     = r.Consignee.Address1,
            address2     = r.Consignee.Address2,
            city         = r.Consignee.City,
            state        = r.Consignee.State,
            zip          = r.Consignee.Zip,
            country      = r.Consignee.Country,
            phone        = r.Consignee.Phone,
            contactName  = r.Consignee.ContactName,
            email        = r.Consignee.Email
        },
        pieces = r.Pieces.Select((p, i) => new
        {
            sequenceNumber  = p.SequenceNumber > 0 ? p.SequenceNumber : i + 1,
            description     = p.Description,
            freightClass    = p.FreightClass,
            weightLbs       = p.WeightLbs,
            lengthIn        = p.LengthIn,
            widthIn         = p.WidthIn,
            heightIn        = p.HeightIn,
            quantity        = p.Quantity,
            unitType        = p.UnitType,
            declaredValue   = p.DeclaredValue,
            isHazmat        = p.IsHazmat
        }).ToList()
    };

    // ── Response mappers ──────────────────────────────────────────────────────

    private static CreateOrderResponse MapCreateOrderResponse(MetroCreateOrderResponse m) => new()
    {
        TrackingNumber      = m.TrackingNumber,
        OrderNumber         = m.OrderNumber,
        ReferenceNumber     = m.ReferenceNumber,
        MasterBarcodeBase64 = m.MasterBarcodeBase64,
        MasterBarcodeValue  = m.MasterBarcodeValue,
        TotalWeightLbs      = m.TotalWeightLbs,
        TotalPieces         = m.TotalPieces,
        EstimatedCharges    = m.EstimatedCharges,
        EstimatedDeliveryDate = m.EstimatedDeliveryDate,
        OrderCreatedAtUtc   = m.OrderCreatedAtUtc,
        ServiceDescription  = m.ServiceDescription,
        Messages            = m.Messages ?? new List<string>(),
        Pieces = (m.Pieces ?? new List<MetroPieceDetail>()).Select(p => new PieceDetail
        {
            SequenceNumber      = p.SequenceNumber,
            PieceBarcode        = p.PieceBarcode,
            PieceBarcodeBase64  = p.PieceBarcodeBase64,
            Description         = p.Description,
            WeightLbs           = p.WeightLbs,
            FreightClass        = p.FreightClass,
            Quantity            = p.Quantity,
            UnitType            = p.UnitType,
            Dimensions          = p.Dimensions
        }).ToList()
    };

    private static GetOrderLabelsResponse MapGetLabelsResponse(
        MetroGetLabelsResponse m,
        GetOrderLabelsRequest request) => new()
    {
        TrackingNumber          = m.TrackingNumber,
        CombinedLabelsPdfBase64 = m.CombinedLabelsPdfBase64,
        LabelFormat             = request.LabelFormat,
        LabelSize               = request.LabelSize,
        GeneratedAtUtc          = DateTime.UtcNow,
        PieceLabels = (m.PieceLabels ?? new List<MetroPieceLabelDetail>()).Select(p => new PieceLabelDetail
        {
            SequenceNumber  = p.SequenceNumber,
            PieceBarcode    = p.PieceBarcode,
            LabelPdfBase64  = p.LabelPdfBase64
        }).ToList()
    };

    private static GetOrderBOLResponse MapGetBolResponse(MetroGetBolResponse m) => new()
    {
        TrackingNumber  = m.TrackingNumber,
        BolPdfBase64    = m.BolPdfBase64,
        BolNumber       = m.BolNumber,
        Copies          = m.Copies,
        GeneratedAtUtc  = DateTime.UtcNow,
        ShipperName     = m.ShipperName,
        ConsigneeName   = m.ConsigneeName,
        TotalWeightLbs  = m.TotalWeightLbs,
        TotalPieces     = m.TotalPieces
    };
}

// ─────────────────────────────────────────────────────────────────────────────
// Internal Metro API contract types (not exposed outside this file)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Raw response shape returned by Metro's CreateOrder endpoint.</summary>
file class MetroCreateOrderResponse
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? MasterBarcodeBase64 { get; set; }
    public string? MasterBarcodeValue { get; set; }
    public decimal TotalWeightLbs { get; set; }
    public int TotalPieces { get; set; }
    public decimal? EstimatedCharges { get; set; }
    public DateTime? EstimatedDeliveryDate { get; set; }
    public DateTime OrderCreatedAtUtc { get; set; }
    public string? ServiceDescription { get; set; }
    public List<string>? Messages { get; set; }
    public List<MetroPieceDetail>? Pieces { get; set; }
}

file class MetroPieceDetail
{
    public int SequenceNumber { get; set; }
    public string PieceBarcode { get; set; } = string.Empty;
    public string? PieceBarcodeBase64 { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal WeightLbs { get; set; }
    public string? FreightClass { get; set; }
    public int Quantity { get; set; }
    public string UnitType { get; set; } = string.Empty;
    public string? Dimensions { get; set; }
}

file class MetroGetLabelsResponse
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string CombinedLabelsPdfBase64 { get; set; } = string.Empty;
    public List<MetroPieceLabelDetail>? PieceLabels { get; set; }
}

file class MetroPieceLabelDetail
{
    public int SequenceNumber { get; set; }
    public string PieceBarcode { get; set; } = string.Empty;
    public string LabelPdfBase64 { get; set; } = string.Empty;
}

file class MetroGetBolResponse
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string BolPdfBase64 { get; set; } = string.Empty;
    public string? BolNumber { get; set; }
    public int Copies { get; set; }
    public string? ShipperName { get; set; }
    public string? ConsigneeName { get; set; }
    public decimal? TotalWeightLbs { get; set; }
    public int? TotalPieces { get; set; }
}

file class MetroErrorResponse
{
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
