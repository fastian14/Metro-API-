namespace MetroAPI.Configuration;

/// <summary>
/// Strongly-typed configuration for the Metropolitan carrier API.
/// Populated from appsettings.json → "MetroApi" section.
/// </summary>
public class MetroApiSettings
{
    public const string SectionName = "MetroApi";

    /// <summary>Base URL of the Metropolitan carrier API (no trailing slash).</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>API key / token used for authenticating against the Metro API.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Metro account number assigned to SparsWeb.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>HTTP request timeout in seconds (default: 30).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    // --- Endpoint paths (relative to BaseUrl) ---

    /// <summary>Relative path for the CreateOrder endpoint.</summary>
    public string CreateOrderPath { get; set; } = "/api/orders/create";

    /// <summary>Relative path for the GetOrderLabels endpoint.</summary>
    public string GetOrderLabelsPath { get; set; } = "/api/orders/labels";

    /// <summary>Relative path for the GetOrderBOL endpoint.</summary>
    public string GetOrderBOLPath { get; set; } = "/api/orders/bol";
}
