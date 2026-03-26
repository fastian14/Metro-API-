using System.ComponentModel.DataAnnotations;

namespace MetroAPI.Models.Requests;

/// <summary>
/// Request to retrieve one or more shipping labels (Base64-encoded PDF)
/// for an existing Metro order.
/// Metro endpoint: GetOrderLabels
/// </summary>
public class GetOrderLabelsRequest
{
    /// <summary>
    /// The Metro PRO / tracking number returned by CreateOrder.
    /// Required to look up the order.
    /// </summary>
    [Required]
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// Optional: retrieve labels only for specific piece barcodes.
    /// When empty, labels for ALL pieces on the order are returned.
    /// </summary>
    public List<string>? PieceBarcodes { get; set; }

    /// <summary>
    /// Label format. Supported values: "PDF" (default), "ZPL", "PNG".
    /// </summary>
    public string LabelFormat { get; set; } = "PDF";

    /// <summary>
    /// Label size. Supported values: "4x6" (default), "8.5x11".
    /// </summary>
    public string LabelSize { get; set; } = "4x6";
}
