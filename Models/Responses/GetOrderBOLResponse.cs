namespace MetroAPI.Models.Responses;

/// <summary>
/// Response returned by GetOrderBOL.
/// Contains the Bill of Lading as a Base64-encoded PDF.
/// </summary>
public class GetOrderBOLResponse
{
    /// <summary>The Metro PRO / tracking number this BOL belongs to.</summary>
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// Bill of Lading document as a Base64-encoded PDF.
    /// Decode and save as a .pdf file, or stream directly to the browser.
    /// </summary>
    public string BolPdfBase64 { get; set; } = string.Empty;

    /// <summary>
    /// Metro-assigned BOL number (printed on the document).
    /// May differ from the PRO number.
    /// </summary>
    public string? BolNumber { get; set; }

    /// <summary>Number of copies included in the PDF.</summary>
    public int Copies { get; set; }

    /// <summary>Timestamp when the BOL document was generated (UTC).</summary>
    public DateTime GeneratedAtUtc { get; set; }

    /// <summary>Shipper name as printed on the BOL.</summary>
    public string? ShipperName { get; set; }

    /// <summary>Consignee name as printed on the BOL.</summary>
    public string? ConsigneeName { get; set; }

    /// <summary>Total shipment weight (lbs) as printed on the BOL.</summary>
    public decimal? TotalWeightLbs { get; set; }

    /// <summary>Total number of pieces as printed on the BOL.</summary>
    public int? TotalPieces { get; set; }
}
