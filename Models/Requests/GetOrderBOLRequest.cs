using System.ComponentModel.DataAnnotations;

namespace MetroAPI.Models.Requests;

/// <summary>
/// Request to retrieve the Bill of Lading (Base64-encoded PDF)
/// for an existing Metro order.
/// Metro endpoint: GetOrderBOL
/// </summary>
public class GetOrderBOLRequest
{
    /// <summary>
    /// The Metro PRO / tracking number returned by CreateOrder.
    /// Required to look up the order.
    /// </summary>
    [Required]
    public string TrackingNumber { get; set; } = string.Empty;

    /// <summary>
    /// Number of copies to include in the returned PDF (default: 1).
    /// Some carriers support multi-copy BOLs in a single document.
    /// </summary>
    public int Copies { get; set; } = 1;
}
