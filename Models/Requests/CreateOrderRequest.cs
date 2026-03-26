using System.ComponentModel.DataAnnotations;

namespace MetroAPI.Models.Requests;

/// <summary>
/// Request payload sent by SparsWeb to create a new Metro shipment order.
/// Metro endpoint: CreateOrder
/// </summary>
public class CreateOrderRequest
{
    // ── Account ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Metro account number. If omitted, falls back to the configured default
    /// account number in appsettings.json.
    /// </summary>
    public string? AccountNumber { get; set; }

    // ── Shipper ───────────────────────────────────────────────────────────────

    [Required]
    public ShipperInfo Shipper { get; set; } = new();

    // ── Consignee ─────────────────────────────────────────────────────────────

    [Required]
    public ConsigneeInfo Consignee { get; set; } = new();

    // ── Shipment Details ──────────────────────────────────────────────────────

    /// <summary>Requested delivery service type (e.g. "STD", "EXP", "SAT").</summary>
    [Required]
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>SparsWeb internal reference / order number for cross-referencing.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Purchase order number for the consignee.</summary>
    public string? PurchaseOrderNumber { get; set; }

    /// <summary>Free-text special instructions for the driver/warehouse.</summary>
    public string? SpecialInstructions { get; set; }

    /// <summary>Requested pickup date. Defaults to today when not supplied.</summary>
    public DateTime? PickupDate { get; set; }

    /// <summary>
    /// Whether the delivery address is residential.
    /// Affects accessorial charges.
    /// </summary>
    public bool IsResidential { get; set; } = false;

    /// <summary>
    /// Whether a lift-gate is required at delivery.
    /// </summary>
    public bool LiftGateRequired { get; set; } = false;

    /// <summary>
    /// Whether inside delivery is required.
    /// </summary>
    public bool InsideDelivery { get; set; } = false;

    // ── Pieces ────────────────────────────────────────────────────────────────

    /// <summary>
    /// One or more pieces / packages that make up this shipment.
    /// Metro generates one barcode per piece.
    /// </summary>
    [Required]
    [MinLength(1, ErrorMessage = "At least one piece is required.")]
    public List<PieceInfo> Pieces { get; set; } = new();
}

// ── Nested types ─────────────────────────────────────────────────────────────

public class ShipperInfo
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Address1 { get; set; } = string.Empty;
    public string? Address2 { get; set; }
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string State { get; set; } = string.Empty;
    [Required] public string Zip { get; set; } = string.Empty;
    public string Country { get; set; } = "US";
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
}

public class ConsigneeInfo
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public string Address1 { get; set; } = string.Empty;
    public string? Address2 { get; set; }
    [Required] public string City { get; set; } = string.Empty;
    [Required] public string State { get; set; } = string.Empty;
    [Required] public string Zip { get; set; } = string.Empty;
    public string Country { get; set; } = "US";
    public string? Phone { get; set; }
    public string? ContactName { get; set; }
    public string? Email { get; set; }
}

public class PieceInfo
{
    /// <summary>Piece sequence number (1-based). Metro uses this in barcode generation.</summary>
    public int SequenceNumber { get; set; }

    /// <summary>Description of goods in this piece.</summary>
    [Required]
    public string Description { get; set; } = string.Empty;

    /// <summary>Commodity / freight class (e.g. "50", "70", "85").</summary>
    public string? FreightClass { get; set; }

    /// <summary>Weight in pounds.</summary>
    [Range(0.01, 99999.99)]
    public decimal WeightLbs { get; set; }

    /// <summary>Length in inches.</summary>
    public decimal? LengthIn { get; set; }

    /// <summary>Width in inches.</summary>
    public decimal? WidthIn { get; set; }

    /// <summary>Height in inches.</summary>
    public decimal? HeightIn { get; set; }

    /// <summary>Number of units inside this piece.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Unit type (e.g. "PLT", "CTN", "PKG").</summary>
    public string UnitType { get; set; } = "CTN";

    /// <summary>Declared value for this piece (for insurance / liability).</summary>
    public decimal? DeclaredValue { get; set; }

    /// <summary>Hazardous material flag.</summary>
    public bool IsHazmat { get; set; } = false;
}
