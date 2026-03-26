using System.Text.Json;
using System.Text.Json.Serialization;
using MetroAPI.Configuration;
using Microsoft.Extensions.Options;

namespace MetroAPI.Services;

/// <summary>
/// Singleton service that authenticates with the Metropolitan carrier API
/// using an OAuth2 password-grant flow and caches the resulting bearer token.
///
/// Token endpoint: POST <see cref="MetroApiSettings.TokenUrl"/>
/// Content-Type  : application/x-www-form-urlencoded
/// Body fields   : grant_type, username, password, client_id
///
/// The token is cached in memory and proactively refreshed
/// <see cref="MetroApiSettings.TokenEarlyRefreshSeconds"/> seconds before
/// it expires (Metro tokens last ~24 hours / 86 399 s).
///
/// Thread safety: a <see cref="SemaphoreSlim"/> prevents concurrent
/// token-refresh storms when multiple requests arrive simultaneously on
/// a cold start or near expiry.
/// </summary>
public sealed class MetroTokenService : IMetroTokenService, IDisposable
{
    private readonly HttpClient _http;           // created once from the named factory
    private readonly MetroApiSettings _settings;
    private readonly ILogger<MetroTokenService> _logger;

    // ── Cache ─────────────────────────────────────────────────────────────────
    private string? _cachedToken;
    private DateTime _tokenExpiresAt = DateTime.MinValue;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public MetroTokenService(
        IHttpClientFactory httpClientFactory,
        IOptions<MetroApiSettings> settings,
        ILogger<MetroTokenService> logger)
    {
        // Create one long-lived HttpClient from the named registration "MetroToken".
        // Safe for singletons: IHttpClientFactory manages the underlying handler lifetime.
        _http     = httpClientFactory.CreateClient("MetroToken");
        _settings = settings.Value;
        _logger   = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<string> GetAccessTokenAsync(CancellationToken ct = default)
    {
        // Fast path — no lock needed for a simple staleness check
        if (IsTokenValid())
        {
            _logger.LogDebug("Using cached Metro access token (expires {Expiry:u})", _tokenExpiresAt);
            return _cachedToken!;
        }

        // Slow path — acquire the semaphore then re-check (double-checked locking)
        await _lock.WaitAsync(ct);
        try
        {
            if (IsTokenValid())
                return _cachedToken!;

            await FetchTokenAsync(ct);
            return _cachedToken!;
        }
        finally
        {
            _lock.Release();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Private helpers
    // ─────────────────────────────────────────────────────────────────────────

    private bool IsTokenValid() =>
        _cachedToken is not null &&
        DateTime.UtcNow < _tokenExpiresAt;

    private async Task FetchTokenAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "Fetching new Metro access token from {TokenUrl}", _settings.TokenUrl);

        // Build form-urlencoded body exactly as the Metro API expects
        var formFields = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"]   = _settings.Username,
            ["password"]   = _settings.Password,
            ["client_id"]  = _settings.ClientId
        };

        using var formContent = new FormUrlEncodedContent(formFields);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsync(_settings.TokenUrl, formContent, ct);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Network error reaching Metro token endpoint");
            throw new MetroApiException("AUTH_NETWORK_ERROR",
                $"Could not reach the Metro token endpoint: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogError(ex, "Metro token request timed out");
            throw new MetroApiException("AUTH_TIMEOUT",
                "The Metro token endpoint did not respond within the configured timeout.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Metro token endpoint returned HTTP {Status}: {Body}",
                (int)response.StatusCode, body);

            throw new MetroApiException("AUTH_ERROR",
                $"Metro authentication failed (HTTP {(int)response.StatusCode}): {body}");
        }

        // Parse the OAuth2 token response
        MetroTokenResponse tokenResponse;
        try
        {
            tokenResponse = JsonSerializer.Deserialize<MetroTokenResponse>(body, _jsonOptions)
                            ?? throw new InvalidOperationException("Null token response");
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger.LogError(ex, "Failed to parse Metro token response: {Body}", body);
            throw new MetroApiException("AUTH_PARSE_ERROR",
                "Could not parse the Metro token response.");
        }

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            _logger.LogError("Metro token response contained an empty access_token");
            throw new MetroApiException("AUTH_EMPTY_TOKEN",
                "Metro returned an empty access token.");
        }

        // Cache the token; refresh slightly before actual expiry
        var earlyRefresh = TimeSpan.FromSeconds(_settings.TokenEarlyRefreshSeconds);
        var lifetime     = TimeSpan.FromSeconds(tokenResponse.ExpiresIn);
        _cachedToken     = tokenResponse.AccessToken;
        _tokenExpiresAt  = DateTime.UtcNow + lifetime - earlyRefresh;

        _logger.LogInformation(
            "Metro access token acquired. UserId={Id} ExpiresIn={ExpiresIn}s CachedUntil={CachedUntil:u}",
            tokenResponse.Id, tokenResponse.ExpiresIn, _tokenExpiresAt);
    }

    public void Dispose() => _lock.Dispose();
}

// ── Internal token response contract ─────────────────────────────────────────

/// <summary>
/// Matches the JSON shape returned by POST /order/token:
/// <code>
/// {
///   "access_token": "…",
///   "token_type":   "bearer",
///   "expires_in":   86399,
///   "id":           "75",
///   ".issued":      "Wed, 14 Oct 2015 11:15:15 GMT",
///   ".expires":     "Thu, 15 Oct 2015 11:15:14 GMT"
/// }
/// </code>
/// </summary>
file sealed class MetroTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName(".issued")]
    public string? Issued { get; set; }

    [JsonPropertyName(".expires")]
    public string? Expires { get; set; }
}
