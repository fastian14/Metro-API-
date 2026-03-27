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
        _logger.LogInformation(
            "CreateOrder → ClientRef1={Ref} Origin={Origin} Items={Items}",
            request.ClientRef1, request.OriginCompany, request.Item?.Count ?? 0);

        // Map our SparsWeb request model to the Metro API contract
        var metroPayload = BuildCreateOrderPayload(request);

        // Metro wraps all responses in { status, message, result: { asnOrder: [...] } }
        var apiResponse = await PostAsync<MetroCreateOrderApiResponse>(
            _settings.CreateOrderPath, metroPayload, ct);

        if (!string.Equals(apiResponse.Status, "Success", StringComparison.OrdinalIgnoreCase))
            throw new MetroApiException("ORDER_FAILED",
                apiResponse.Message ?? "Metro rejected the order.");

        var order = apiResponse.Result?.AsnOrder?.FirstOrDefault()
            ?? throw new MetroApiException("EMPTY_RESPONSE",
                "Metro returned no order data in the response.");

        _logger.LogInformation(
            "CreateOrder succeeded → TrackingNumber={Tracking} PON={Pon} TotalQty={Qty}",
            order.TrackingNumber, order.Pon, order.TotalQty);

        return MapCreateOrderResponse(apiResponse.Status!, apiResponse.Message, order);
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

    /// <summary>
    /// Wraps the order in Metro's required envelope: { "asnOrder": [ { ...order fields... } ] }
    /// Field names match the Metro API contract exactly (including non-standard casing).
    /// </summary>
    private static object BuildCreateOrderPayload(CreateOrderRequest r) => new
    {
        asnOrder = new[]
        {
            new
            {
                clientKey                  = r.ClientKey                  ?? "",
                originCompany              = r.OriginCompany              ?? "",
                originAddress              = r.OriginAddress              ?? "",
                originAddress2             = r.OriginAddress2             ?? "",
                originZip                  = r.OriginZip                  ?? "",
                originState                = r.OriginState                ?? "",
                originCity                 = r.OriginCity                 ?? "",
                originCountry              = r.OriginCountry              ?? "US",
                originContactPerson        = r.OriginContactPerson        ?? "",
                originEmail                = r.OriginEmail                ?? "",
                originPhone                = r.OriginPhone                ?? "",
                originExt                  = r.OriginExt                  ?? "",
                originIsMilitaryBase       = r.OriginIsMilitaryBase       ?? "0",
                destinationFirstName       = r.DestinationFirstName       ?? "",
                destinationLastName        = r.DestinationLastName        ?? "",
                destinationCompany         = r.DestinationCompany         ?? "",
                destinationAddress         = r.DestinationAddress         ?? "",
                destinationAddress2        = r.DestinationAddress2        ?? "",
                destinationZip             = r.DestinationZip             ?? "",
                destinationState           = r.DestinationState           ?? "",
                destinationCity            = r.DestinationCity            ?? "",
                destinationCountry         = r.DestinationCountry         ?? "US",
                destinationContactPerson   = r.DestinationContactPerson   ?? "",
                destinationEmail           = r.DestinationEmail           ?? "",
                destinationPhone           = r.DestinationPhone           ?? "",
                destinationExt             = r.DestinationExt             ?? "",
                destinationMobile          = r.DestinationMobile          ?? "",
                destinationIsMilitaryBase  = r.DestinationIsMilitaryBase  ?? "0",
                carrierName                = r.CarrierName                ?? "",
                carrierPRO                 = r.CarrierPRO                 ?? "",
                tag                        = r.Tag                        ?? "",
                clientRef1                 = r.ClientRef1                 ?? "",
                clientRef2                 = r.ClientRef2                 ?? "",
                typeofpickup               = r.TypeOfPickup               ?? "",
                pickupDate                 = r.PickupDate                 ?? "",
                operatingHoursFrom         = r.OperatingHoursFrom         ?? "",
                operatingHoursTo           = r.OperatingHoursTo           ?? "",
                deliverByDate              = r.DeliverByDate              ?? "",
                typeofDelivery             = r.TypeOfDelivery             ?? "",
                deliveryoperatingHoursFrom = r.DeliveryOperatingHoursFrom ?? "",
                deliveryoperatingHoursTo   = r.DeliveryOperatingHoursTo   ?? "",
                itemstoShip                = r.ItemsToShip                ?? "",
                specialInstruction         = r.SpecialInstruction         ?? "",
                pickupInstruction          = r.PickupInstruction          ?? "",
                priority                   = r.Priority                   ?? "",
                quoteID                    = r.QuoteID                    ?? "",
                quoteAmount                = r.QuoteAmount                ?? "",
                tariffID                   = r.TariffID                   ?? "",
                additionalOrderParams      = r.AdditionalOrderParams      ?? new List<object>(),
                freightCollect             = r.FreightCollect             ?? "0",
                freightBillingInfo         = BuildFreightBillingPayload(r.FreightBillingInfo),
                item = (r.Item ?? new List<OrderItem>()).Select(i => new
                {
                    type                 = i.Type                 ?? "",
                    pkgQty               = i.PkgQty               ?? "",
                    itemDescription      = i.ItemDescription      ?? "",
                    skuNo                = i.SkuNo                ?? "",
                    clientItemRef        = i.ClientItemRef        ?? "",
                    additionalItemRef    = i.AdditionalItemRef    ?? "",
                    packedbyShipper      = i.PackedByShipper      ?? "",
                    weight               = i.Weight               ?? "",
                    value                = i.Value                ?? "",
                    dim_Length           = i.DimLength            ?? "",
                    dim_Width            = i.DimWidth             ?? "",
                    dim_Height           = i.DimHeight            ?? "",
                    assemblyTime         = i.AssemblyTime         ?? "",
                    additionalItemParams = i.AdditionalItemParams ?? new List<object>()
                }).ToList()
            }
        }
    };

    private static object BuildFreightBillingPayload(FreightBillingInfo? f) => new
    {
        thirdPartyBillTo = f?.ThirdPartyBillTo ?? "",
        contactPerson    = f?.ContactPerson    ?? "",
        company          = f?.Company          ?? "",
        address1         = f?.Address1         ?? "",
        address2         = f?.Address2         ?? "",
        zip              = f?.Zip              ?? "",
        city             = f?.City             ?? "",
        state            = f?.State            ?? "",
        country          = f?.Country          ?? "US",
        email            = f?.Email            ?? "",
        phone            = f?.Phone            ?? "",
        ext              = f?.Ext              ?? "",
        mobile           = f?.Mobile           ?? ""
    };

    // ── Response mappers ──────────────────────────────────────────────────────

    private static CreateOrderResponse MapCreateOrderResponse(
        string status, string? message, MetroCreateOrderOrder o) => new()
    {
        Status                = status,
        Message               = message,
        TrackingNumber        = o.TrackingNumber   ?? string.Empty,
        Pon                   = o.Pon,
        BillTo                = o.BillTo,
        OriginHub             = o.OriginHub,
        OriginHubZip          = o.OriginHubZip,
        DestinationHub        = o.DestinationHub,
        DestinationHubZip     = o.DestinationHubZip,
        OriginCompany         = o.OriginCompany,
        OriginContactPerson   = o.OriginContactPerson,
        OriginAddress         = o.OriginAddress,
        OriginAddress2        = o.OriginAddress2,
        OriginZip             = o.OriginZip,
        OriginState           = o.OriginState,
        OriginCity            = o.OriginCity,
        OriginCountry         = o.OriginCountry,
        OriginPhone           = o.OriginPhone,
        OriginExt             = o.OriginExt,
        DestinationCompany    = o.DestinationCompany,
        DestinationName       = o.DestinationName,
        DestinationAddress    = o.DestinationAddress,
        DestinationAddress2   = o.DestinationAddress2,
        DestinationZip        = o.DestinationZip,
        DestinationState      = o.DestinationState,
        DestinationCity       = o.DestinationCity,
        DestinationCountry    = o.DestinationCountry,
        DestinationContactPerson = o.DestinationContactPerson,
        DestinationPhone      = o.DestinationPhone,
        DestinationExt        = o.DestinationExt,
        CarrierName           = o.CarrierName,
        CarrierPRO            = o.CarrierPRO,
        Tag                   = o.Tag,
        ClientRef1            = o.ClientRef1,
        ClientRef2            = o.ClientRef2,
        TypeOfDelivery        = o.TypeofDelivery,
        PickupScheduleType    = o.PickupScheduleType,
        DeliveryScheduleType  = o.DeliveryScheduleType,
        SpecialInstruction    = o.SpecialInstruction,
        ServiceInstruction    = o.ServiceInstruction,
        TotalQty              = o.TotalQty,
        Item = (o.Item ?? new List<MetroPieceDetail>()).Select(p => new OrderItemResult
        {
            PieceNum        = p.PieceNum,
            Barcode         = p.Barcode,
            ItemDescription = p.ItemDescription,
            SkuNo           = p.SkuNo,
            PackedByShipper = p.PackedByShipper,
            Weight          = p.Weight,
            DimLength       = p.DimLength,
            DimWidth        = p.DimWidth,
            DimHeight       = p.DimHeight
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
    // ─────────────────────────────────────────────────────────────────────────
    // Private nested contract types — Metro API response shapes
    // Keeping them nested (not file-scoped) so they can appear in method signatures.
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Top-level envelope returned by Metro's CreateOrder endpoint:
    /// { "status": "Success", "message": "...", "result": { "asnOrder": [...] } }
    /// </summary>
    private sealed class MetroCreateOrderApiResponse
    {
        public string? Status { get; set; }
        public string? Message { get; set; }
        public MetroCreateOrderResult? Result { get; set; }
    }

    private sealed class MetroCreateOrderResult
    {
        public List<MetroCreateOrderOrder>? AsnOrder { get; set; }
    }

    /// <summary>Single order object inside result.asnOrder[].</summary>
    private sealed class MetroCreateOrderOrder
    {
        public string? TrackingNumber { get; set; }
        public string? Pon { get; set; }
        public string? BillTo { get; set; }
        public string? OriginHub { get; set; }
        public string? OriginHubZip { get; set; }
        public string? DestinationHub { get; set; }
        public string? DestinationHubZip { get; set; }
        public string? OriginCompany { get; set; }
        public string? OriginContactPerson { get; set; }
        public string? OriginAddress { get; set; }
        public string? OriginAddress2 { get; set; }
        public string? OriginZip { get; set; }
        public string? OriginState { get; set; }
        public string? OriginCity { get; set; }
        public string? OriginCountry { get; set; }
        public string? OriginPhone { get; set; }
        public string? OriginExt { get; set; }
        public string? DestinationCompany { get; set; }
        public string? DestinationName { get; set; }
        public string? DestinationAddress { get; set; }
        public string? DestinationAddress2 { get; set; }
        public string? DestinationZip { get; set; }
        public string? DestinationState { get; set; }
        public string? DestinationCity { get; set; }
        public string? DestinationCountry { get; set; }
        public string? DestinationContactPerson { get; set; }
        public string? DestinationPhone { get; set; }
        public string? DestinationExt { get; set; }
        public string? CarrierName { get; set; }
        public string? CarrierPRO { get; set; }
        public string? Tag { get; set; }
        public string? ClientRef1 { get; set; }
        public string? ClientRef2 { get; set; }
        /// <summary>Metro returns "typeofDelivery" (lowercase 'of').</summary>
        public string? TypeofDelivery { get; set; }
        public string? PickupScheduleType { get; set; }
        public string? DeliveryScheduleType { get; set; }
        public string? SpecialInstruction { get; set; }
        public string? ServiceInstruction { get; set; }
        public string? TotalQty { get; set; }
        public List<MetroPieceDetail>? Item { get; set; }
    }

    private sealed class MetroPieceDetail
    {
        public string? PieceNum { get; set; }
        public string? Barcode { get; set; }
        public string? ItemDescription { get; set; }
        /// <summary>Metro returns "skuno"; case-insensitive binding handles the 'N' difference.</summary>
        public string? SkuNo { get; set; }
        public string? PackedByShipper { get; set; }
        public string? Weight { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("dim_Length")]
        public string? DimLength { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("dim_Width")]
        public string? DimWidth { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("dim_Height")]
        public string? DimHeight { get; set; }
    }

    private sealed class MetroGetLabelsResponse
    {
        public string TrackingNumber { get; set; } = string.Empty;
        public string CombinedLabelsPdfBase64 { get; set; } = string.Empty;
        public List<MetroPieceLabelDetail>? PieceLabels { get; set; }
    }

    private sealed class MetroPieceLabelDetail
    {
        public int SequenceNumber { get; set; }
        public string PieceBarcode { get; set; } = string.Empty;
        public string LabelPdfBase64 { get; set; } = string.Empty;
    }

    private sealed class MetroGetBolResponse
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

    private sealed class MetroErrorResponse
    {
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
