namespace MetroAPI.Models.Responses;

/// <summary>
/// Response returned to SparsWeb after a successful CreateOrder call.
/// Mapped 1-to-1 from the Metro API result: { status, message, result.asnOrder[0] }
/// </summary>
public class CreateOrderResponse
{
    // ── Top-level status ──────────────────────────────────────────────────────
    /// <summary>"Success" or error string returned by Metro.</summary>
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }

    // ── Order identifiers ─────────────────────────────────────────────────────
    /// <summary>
    /// Master tracking number (PRO). Use this for GetOrderLabels and GetOrderBOL.
    /// </summary>
    public string TrackingNumber { get; set; } = string.Empty;
    /// <summary>Metro purchase/pick order number (PON).</summary>
    public string? Pon { get; set; }
    /// <summary>Billing account name confirmed by Metro.</summary>
    public string? BillTo { get; set; }

    // ── Hub routing ───────────────────────────────────────────────────────────
    public string? OriginHub { get; set; }
    public string? OriginHubZip { get; set; }
    public string? DestinationHub { get; set; }
    public string? DestinationHubZip { get; set; }

    // ── Origin echo ───────────────────────────────────────────────────────────
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

    // ── Destination echo ──────────────────────────────────────────────────────
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

    // ── References & carrier ──────────────────────────────────────────────────
    public string? CarrierName { get; set; }
    public string? CarrierPRO { get; set; }
    public string? Tag { get; set; }
    public string? ClientRef1 { get; set; }
    public string? ClientRef2 { get; set; }

    // ── Schedule ──────────────────────────────────────────────────────────────
    public string? TypeOfDelivery { get; set; }
    public string? PickupScheduleType { get; set; }
    public string? DeliveryScheduleType { get; set; }

    // ── Instructions ──────────────────────────────────────────────────────────
    public string? SpecialInstruction { get; set; }
    public string? ServiceInstruction { get; set; }

    // ── Totals ────────────────────────────────────────────────────────────────
    /// <summary>Total piece count confirmed by Metro (string per API).</summary>
    public string? TotalQty { get; set; }

    // ── Items (with barcodes) ─────────────────────────────────────────────────
    /// <summary>
    /// Per-piece results including the barcode needed for GetOrderLabels.
    /// SparsWeb should store each barcode alongside the tracking number.
    /// </summary>
    public List<OrderItemResult> Item { get; set; } = new();
}

// ── Nested types ─────────────────────────────────────────────────────────────

public class OrderItemResult
{
    /// <summary>1-based piece sequence number (string per Metro API).</summary>
    public string? PieceNum { get; set; }
    /// <summary>
    /// Individual piece barcode (e.g. "0004803995140001").
    /// Used to retrieve or reprint a single piece label via GetOrderLabels.
    /// </summary>
    public string? Barcode { get; set; }
    public string? ItemDescription { get; set; }
    /// <summary>SKU number (Metro returns this as "skuno").</summary>
    public string? SkuNo { get; set; }
    /// <summary>"1" = packed by shipper, "0" = not packed by shipper.</summary>
    public string? PackedByShipper { get; set; }
    public string? Weight { get; set; }
    public string? DimLength { get; set; }
    public string? DimWidth { get; set; }
    public string? DimHeight { get; set; }
}
