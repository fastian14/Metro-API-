namespace MetroAPI.Services;

/// <summary>
/// Provides a valid Metro API bearer token, refreshing it transparently when
/// it nears expiry. Implementations are expected to be singleton-scoped so
/// the cached token is shared across all requests.
/// </summary>
public interface IMetroTokenService
{
    /// <summary>
    /// Returns a valid access token, fetching or refreshing it as needed.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken ct = default);
}
