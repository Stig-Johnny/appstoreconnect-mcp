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

    /// <summary>
    /// Gets a specific artifact's details including the download URL.
    /// </summary>
    public async Task<JsonDocument> GetArtifactAsync(string artifactId, CancellationToken cancellationToken = default)
    {
        return await GetAsync($"/ciArtifacts/{artifactId}", cancellationToken);
    }

    /// <summary>
    /// Downloads a file from a given URL (used for artifact downloads).
    /// </summary>
    public async Task<byte[]> DownloadFileAsync(string url, CancellationToken cancellationToken = default)
    {
        var token = GenerateToken();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Gets build logs for a specific action by downloading and parsing the LOG_BUNDLE artifact.
    /// </summary>
    public async Task<string> GetBuildLogsAsync(string actionId, int tailLines = 500, CancellationToken cancellationToken = default)
    {
        // Step 1: List artifacts for this action
        var artifactsDoc = await ListArtifactsAsync(actionId, cancellationToken);
        var artifacts = artifactsDoc.RootElement.GetProperty("data");

        // Step 2: Find the LOG_BUNDLE artifact
        string? logArtifactId = null;
        foreach (var artifact in artifacts.EnumerateArray())
        {
            var artifactType = artifact.GetProperty("attributes").GetProperty("fileType").GetString();
            if (artifactType == "LOG_BUNDLE")
            {
                logArtifactId = artifact.GetProperty("id").GetString();
                break;
            }
        }

        if (logArtifactId == null)
        {
            return "No LOG_BUNDLE artifact found for this action.";
        }

        // Step 3: Get the artifact's download URL
        var artifactDoc = await GetArtifactAsync(logArtifactId, cancellationToken);
        var downloadUrl = artifactDoc.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("downloadUrl")
            .GetString();

        if (string.IsNullOrEmpty(downloadUrl))
        {
            return "LOG_BUNDLE artifact found but no download URL available.";
        }

        // Step 4: Download the zip file
        byte[] zipData;
        try
        {
            zipData = await DownloadFileAsync(downloadUrl, cancellationToken);
        }
        catch (Exception ex)
        {
            return $"Failed to download log bundle: {ex.Message}";
        }

        // Step 5: Extract and parse the logs
        return ExtractAndParseLogs(zipData, tailLines);
    }

    /// <summary>
    /// Extracts log files from a zip archive and returns relevant content.
    /// </summary>
    private static string ExtractAndParseLogs(byte[] zipData, int tailLines)
    {
        using var zipStream = new MemoryStream(zipData);
        using var archive = new System.IO.Compression.ZipArchive(zipStream, System.IO.Compression.ZipArchiveMode.Read);

        var result = new System.Text.StringBuilder();
        var relevantFiles = new List<(string Name, string Content)>();

        foreach (var entry in archive.Entries)
        {
            // Look for relevant log files
            var name = entry.FullName.ToLowerInvariant();
            if (name.EndsWith(".log") || name.EndsWith(".txt") ||
                name.Contains("xcodebuild") || name.Contains("error") ||
                name.Contains("build") || name.Contains("archive"))
            {
                try
                {
                    using var stream = entry.Open();
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();

                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        relevantFiles.Add((entry.FullName, content));
                    }
                }
                catch
                {
                    // Skip files that can't be read
                }
            }
        }

        if (relevantFiles.Count == 0)
        {
            result.AppendLine("No readable log files found in the archive.");
            result.AppendLine("\nArchive contents:");
            foreach (var entry in archive.Entries.Take(50))
            {
                result.AppendLine($"  - {entry.FullName}");
            }
            return result.ToString();
        }

        // Prioritize files that likely contain errors
        var prioritized = relevantFiles
            .OrderByDescending(f => f.Name.Contains("error", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Name.Contains("xcodebuild", StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(f => f.Content.Contains("error:", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var (fileName, content) in prioritized.Take(5))
        {
            result.AppendLine($"=== {fileName} ===");

            // Get the last N lines
            var lines = content.Split('\n');
            var startIndex = Math.Max(0, lines.Length - tailLines);
            var selectedLines = lines.Skip(startIndex).ToArray();

            // Also extract any error lines from earlier in the file
            var errorLines = lines.Take(startIndex)
                .Where(l => l.Contains("error:", StringComparison.OrdinalIgnoreCase) ||
                           l.Contains("fatal:", StringComparison.OrdinalIgnoreCase) ||
                           l.Contains("failed:", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (errorLines.Length > 0)
            {
                result.AppendLine("--- Errors found earlier in log ---");
                foreach (var line in errorLines.Take(50))
                {
                    result.AppendLine(line);
                }
                result.AppendLine("--- End of earlier errors ---\n");
            }

            result.AppendLine(string.Join("\n", selectedLines));
            result.AppendLine();
        }

        return result.ToString();
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
