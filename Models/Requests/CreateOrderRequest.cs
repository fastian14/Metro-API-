namespace MetroAPI.Models.Requests;

/// <summary>
/// Represents a single Metro shipment order submitted by SparsWeb.
/// The service wraps this in the Metro API envelope: { "asnOrder": [ ... ] }
/// All fields are optional strings to match the Metro API contract exactly.
/// </summary>
public class CreateOrderRequest
{
    // ── Account ───────────────────────────────────────────────────────────────
    public string? ClientKey { get; set; }

    // ── Origin (Shipper) ──────────────────────────────────────────────────────
    public string? OriginCompany { get; set; }
    public string? OriginAddress { get; set; }
    public string? OriginAddress2 { get; set; }
    public string? OriginZip { get; set; }
    public string? OriginState { get; set; }
    public string? OriginCity { get; set; }
    public string? OriginCountry { get; set; }
    public string? OriginContactPerson { get; set; }
    public string? OriginEmail { get; set; }
    public string? OriginPhone { get; set; }
    public string? OriginExt { get; set; }
    /// <summary>"0" = No, "1" = Yes</summary>
    public string? OriginIsMilitaryBase { get; set; }

    // ── Destination (Consignee) ───────────────────────────────────────────────
    public string? DestinationFirstName { get; set; }
    public string? DestinationLastName { get; set; }
    public string? DestinationCompany { get; set; }
    public string? DestinationAddress { get; set; }
    public string? DestinationAddress2 { get; set; }
    public string? DestinationZip { get; set; }
    public string? DestinationState { get; set; }
    public string? DestinationCity { get; set; }
    public string? DestinationCountry { get; set; }
    public string? DestinationContactPerson { get; set; }
    public string? DestinationEmail { get; set; }
    public string? DestinationPhone { get; set; }
    public string? DestinationExt { get; set; }
    public string? DestinationMobile { get; set; }
    /// <summary>"0" = No, "1" = Yes</summary>
    public string? DestinationIsMilitaryBase { get; set; }

    // ── Carrier ───────────────────────────────────────────────────────────────
    public string? CarrierName { get; set; }
    public string? CarrierPRO { get; set; }
    public string? Tag { get; set; }

    // ── References ────────────────────────────────────────────────────────────
    public string? ClientRef1 { get; set; }
    public string? ClientRef2 { get; set; }

    // ── Pickup ────────────────────────────────────────────────────────────────
    /// <summary>e.g. "DOCKBI"</summary>
    public string? TypeOfPickup { get; set; }
    /// <summary>Format: MM/dd/yyyy</summary>
    public string? PickupDate { get; set; }
    public string? OperatingHoursFrom { get; set; }
    public string? OperatingHoursTo { get; set; }

    // ── Delivery ──────────────────────────────────────────────────────────────
    /// <summary>Format: MM/dd/yyyy</summary>
    public string? DeliverByDate { get; set; }
    /// <summary>e.g. "TRHD"</summary>
    public string? TypeOfDelivery { get; set; }
    public string? DeliveryOperatingHoursFrom { get; set; }
    public string? DeliveryOperatingHoursTo { get; set; }

    // ── Shipment ──────────────────────────────────────────────────────────────
    /// <summary>e.g. "NF"</summary>
    public string? ItemsToShip { get; set; }
    public string? SpecialInstruction { get; set; }
    public string? PickupInstruction { get; set; }
    public string? Priority { get; set; }
    public string? QuoteID { get; set; }
    public string? QuoteAmount { get; set; }
    public string? TariffID { get; set; }
    public List<object>? AdditionalOrderParams { get; set; }

    // ── Freight ───────────────────────────────────────────────────────────────
    /// <summary>"0" = Prepaid, "1" = Collect</summary>
    public string? FreightCollect { get; set; }
    public FreightBillingInfo? FreightBillingInfo { get; set; }

    // ── Items ─────────────────────────────────────────────────────────────────
    public List<OrderItem>? Item { get; set; }
}

// ── Nested types ─────────────────────────────────────────────────────────────

public class FreightBillingInfo
{
    public string? ThirdPartyBillTo { get; set; }
    public string? ContactPerson { get; set; }
    public string? Company { get; set; }
    public string? Address1 { get; set; }
    public string? Address2 { get; set; }
    public string? Zip { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Ext { get; set; }
    public string? Mobile { get; set; }
}

public class OrderItem
{
    /// <summary>Item type code, e.g. "C-AR" for Carton – Area Rug.</summary>
    public string? Type { get; set; }
    /// <summary>Package quantity (string per Metro API).</summary>
    public string? PkgQty { get; set; }
    public string? ItemDescription { get; set; }
    public string? SkuNo { get; set; }
    public string? ClientItemRef { get; set; }
    public string? AdditionalItemRef { get; set; }
    /// <summary>"Yes" or "No"</summary>
    public string? PackedByShipper { get; set; }
    /// <summary>Weight in lbs (string per Metro API), e.g. "45.00"</summary>
    public string? Weight { get; set; }
    /// <summary>Declared value (string per Metro API), e.g. "350.00"</summary>
    public string? Value { get; set; }
    /// <summary>Length in inches (string per Metro API)</summary>
    public string? DimLength { get; set; }
    /// <summary>Width in inches (string per Metro API)</summary>
    public string? DimWidth { get; set; }
    /// <summary>Height in inches (string per Metro API)</summary>
    public string? DimHeight { get; set; }
    public string? AssemblyTime { get; set; }
    public List<object>? AdditionalItemParams { get; set; }
}
