using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;

namespace AppStoreConnectMcp;

/// <summary>
/// Client for Apple's App Store Connect API with JWT authentication.
/// </summary>
public class AppStoreConnectClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _keyId;
    private readonly string _issuerId;
    private readonly string _privateKey;
    private string? _cachedToken;
    private DateTime _tokenExpiry;

    private const string BaseUrl = "https://api.appstoreconnect.apple.com/v1";

    public AppStoreConnectClient()
    {
        _httpClient = new HttpClient();

        // Read configuration from environment variables
        _keyId = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_KEY_ID")
            ?? throw new InvalidOperationException("APP_STORE_CONNECT_KEY_ID environment variable is required");
        _issuerId = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_ISSUER_ID")
            ?? throw new InvalidOperationException("APP_STORE_CONNECT_ISSUER_ID environment variable is required");

        // Private key can be provided as file path or direct content
        var keyPath = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_KEY_PATH");
        var keyContent = Environment.GetEnvironmentVariable("APP_STORE_CONNECT_KEY_CONTENT");

        if (!string.IsNullOrEmpty(keyPath) && File.Exists(keyPath))
        {
            _privateKey = File.ReadAllText(keyPath);
        }
        else if (!string.IsNullOrEmpty(keyContent))
        {
            _privateKey = keyContent;
        }
        else
        {
            throw new InvalidOperationException(
                "Either APP_STORE_CONNECT_KEY_PATH or APP_STORE_CONNECT_KEY_CONTENT environment variable is required");
        }
    }

    /// <summary>
    /// Generates a JWT token for App Store Connect API authentication.
    /// </summary>
    private string GenerateToken()
    {
        // Return cached token if still valid (with 1 minute buffer)
        if (_cachedToken != null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-1))
        {
            return _cachedToken;
        }

        var now = DateTime.UtcNow;
        var expiry = now.AddMinutes(20); // Max 20 minutes for ASC API

        // Parse the private key (PEM format)
        var privateKeyText = _privateKey
            .Replace("-----BEGIN PRIVATE KEY-----", "")
            .Replace("-----END PRIVATE KEY-----", "")
            .Replace("\n", "")
            .Replace("\r", "")
            .Trim();

        var privateKeyBytes = Convert.FromBase64String(privateKeyText);
        var ecdsa = ECDsa.Create();
        ecdsa.ImportPkcs8PrivateKey(privateKeyBytes, out _);

        var securityKey = new ECDsaSecurityKey(ecdsa) { KeyId = _keyId };
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.EcdsaSha256);

        var header = new JwtHeader(credentials);
        header["kid"] = _keyId;
        header["typ"] = "JWT";

        var payload = new JwtPayload
        {
            { "iss", _issuerId },
            { "iat", new DateTimeOffset(now).ToUnixTimeSeconds() },
            { "exp", new DateTimeOffset(expiry).ToUnixTimeSeconds() },
            { "aud", "appstoreconnect-v1" }
        };

        var token = new JwtSecurityToken(header, payload);
        var handler = new JwtSecurityTokenHandler();

        _cachedToken = handler.WriteToken(token);
        _tokenExpiry = expiry;

        return _cachedToken;
    }

    /// <summary>
    /// Makes an authenticated GET request to the App Store Connect API.
    /// </summary>
    public async Task<JsonDocument> GetAsync(string endpoint, CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();

        var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}{endpoint}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonDocument.Parse(content);
    }

    /// <summary>
    /// Lists all Xcode Cloud products (apps configured with Xcode Cloud).
    /// </summary>
    public async Task<JsonDocument> ListCiProductsAsync(CancellationToken cancellationToken = default)
    {
        return await GetAsync("/ciProducts", cancellationToken);
    }

    /// <summary>
    /// Lists all builds for a specific Xcode Cloud product, sorted by newest first.
    /// </summary>
    public async Task<JsonDocument> ListBuildsForProductAsync(string productId, int limit = 10, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciProducts/{productId}/buildRuns?limit={limit}&sort=-number", cancellationToken);
    }

    /// <summary>
    /// Gets details for a specific Xcode Cloud build run.
    /// </summary>
    public async Task<JsonDocument> GetBuildRunAsync(string buildRunId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildRuns/{buildRunId}", cancellationToken);
    }

    /// <summary>
    /// Lists all actions for a specific Xcode Cloud build run.
    /// </summary>
    public async Task<JsonDocument> ListBuildActionsAsync(string buildRunId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildRuns/{buildRunId}/actions", cancellationToken);
    }

    /// <summary>
    /// Gets details for a specific build action.
    /// </summary>
    public async Task<JsonDocument> GetBuildActionAsync(string actionId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildActions/{actionId}", cancellationToken);
    }

    /// <summary>
    /// Lists all artifacts for a specific build action.
    /// </summary>
    public async Task<JsonDocument> ListArtifactsAsync(string actionId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildActions/{actionId}/artifacts", cancellationToken);
    }

    /// <summary>
    /// Gets test results for a specific build action.
    /// </summary>
    public async Task<JsonDocument> GetTestResultsAsync(string actionId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildActions/{actionId}/testResults", cancellationToken);
    }

    /// <summary>
    /// Lists issues (errors/warnings) for a specific build action.
    /// </summary>
    public async Task<JsonDocument> ListIssuesAsync(string actionId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciBuildActions/{actionId}/issues", cancellationToken);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
