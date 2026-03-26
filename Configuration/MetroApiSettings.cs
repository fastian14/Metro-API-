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

    // ── OAuth2 password-grant credentials ────────────────────────────────────

    /// <summary>
    /// Full URL of the Metro token endpoint.
    /// POST form-urlencoded: grant_type, username, password, client_id.
    /// </summary>
    public string TokenUrl { get; set; } = "https://stagingapi.gomwd.com/order/token";

    /// <summary>Metro API username (your login).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Metro API password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Metro Customer Number — sent as <c>client_id</c> in the token request.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Number of seconds before the token actually expires at which we proactively
    /// refresh it. Defaults to 300 (5 minutes). Metro tokens last ~24 hours.
    /// </summary>
    public int TokenEarlyRefreshSeconds { get; set; } = 300;

    // ── Shipment account ──────────────────────────────────────────────────────

    /// <summary>Metro account number assigned to SparsWeb.</summary>
    public string AccountNumber { get; set; } = string.Empty;

    /// <summary>HTTP request timeout in seconds (default: 30).</summary>
    public int TimeoutSeconds { get; set; } = 30;

    // ── Endpoint paths (relative to BaseUrl) ─────────────────────────────────

    /// <summary>Relative path for the CreateOrder endpoint.</summary>
    public string CreateOrderPath { get; set; } = "/order/create";

    /// <summary>Relative path for the GetOrderLabels endpoint.</summary>
    public string GetOrderLabelsPath { get; set; } = "/order/labels";

    /// <summary>Relative path for the GetOrderBOL endpoint.</summary>
    public string GetOrderBOLPath { get; set; } = "/order/bol";
}
