namespace MetroAPI.Models.Responses;

/// <summary>
/// Response returned by the CreateOrder call.
/// Contains the Metro tracking number (PRO), order-level barcodes, and
/// per-piece details — all the data SparsWeb needs to store for later
/// label/BOL retrieval.
/// </summary>
public class CreateOrderResponse
{
    // ── Order identifiers ─────────────────────────────────────────────────────

    /// <summary>
    /// Metro PRO number / master tracking number for the entire shipment.
    /// Used as the key for GetOrderLabels and GetOrderBOL.
    /// </summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// Metro internal order / confirmation number.
    /// </summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Echo of the SparsWeb reference number submitted in the request.
    /// </summary>
    public string? ReferenceNumber { get; set; }

    // ── Barcode details ───────────────────────────────────────────────────────

    /// <summary>
    /// Master barcode for the shipment (Code-128 / GS1-128 symbology).
    /// Encoded as a Base64 PNG image.
    /// </summary>
    public string? MasterBarcodeBase64 { get; set; }

    /// <summary>
    /// Raw barcode string value (use to generate your own barcode image
    /// or to store for scanning purposes).
    /// </summary>
    public string? MasterBarcodeValue { get; set; }

    // ── Piece details ─────────────────────────────────────────────────────────

    /// <summary>
    /// One entry per piece submitted in CreateOrder.
    /// Each piece has its own barcode that maps to the master PRO.
    /// </summary>
    public List<PieceDetail> Pieces { get; set; } = new();

    // ── Shipment summary ──────────────────────────────────────────────────────

    /// <summary>Total weight (lbs) accepted by Metro.</summary>
    public decimal TotalWeightLbs { get; set; }

    /// <summary>Total number of pieces in this order.</summary>
    public int TotalPieces { get; set; }

    /// <summary>Estimated freight charges (informational; subject to change on delivery).</summary>
    public decimal? EstimatedCharges { get; set; }

    /// <summary>Estimated delivery date provided by Metro.</summary>
    public DateTime? EstimatedDeliveryDate { get; set; }

    /// <summary>Timestamp when the order was accepted by Metro (UTC).</summary>
    public DateTime OrderCreatedAtUtc { get; set; }

    /// <summary>Metro service description (e.g. "Standard Ground Delivery").</summary>
    public string? ServiceDescription { get; set; }

    /// <summary>Any informational messages or warnings returned by Metro.</summary>
    public List<string> Messages { get; set; } = new();
}

// ── Nested types ─────────────────────────────────────────────────────────────

/// <summary>
/// Per-piece tracking and barcode information returned by CreateOrder.
/// SparsWeb should persist all fields so labels can be re-fetched without
/// calling CreateOrder again.
/// </summary>
public class PieceDetail
{
    /// <summary>1-based sequence number matching the submitted PieceInfo.</summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Unique barcode / scan label for this individual piece.
    /// Required when scanning pieces at the warehouse.
    /// </summary>
    public string PieceBarcode { get; set; } = string.Empty;

    /// <summary>Barcode image rendered as Base64-encoded PNG.</summary>
    public string? PieceBarcodeBase64 { get; set; }

    /// <summary>Description of goods in this piece (echoed from request).</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Accepted weight in pounds (may differ from submitted if re-weighed).</summary>
    public decimal WeightLbs { get; set; }

    /// <summary>Freight class assigned by Metro.</summary>
    public string? FreightClass { get; set; }

    /// <summary>Number of units in this piece.</summary>
    public int Quantity { get; set; }

    /// <summary>Unit type (e.g. "PLT", "CTN", "PKG").</summary>
    public string UnitType { get; set; } = string.Empty;

    /// <summary>Dimensions accepted by Metro (L × W × H in inches).</summary>
    public string? Dimensions { get; set; }
}
