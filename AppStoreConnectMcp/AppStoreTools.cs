using System.ComponentModel;
using System.Security.Cryptography;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace AppStoreConnectMcp;

/// <summary>
/// MCP tools for App Store submission operations via App Store Connect API.
/// </summary>
[McpServerToolType]
public class AppStoreTools
{
    private readonly AppStoreConnectClient _client;

    public AppStoreTools(AppStoreConnectClient client)
    {
        _client = client;
    }

    // ==================== Apps ====================

    [McpServerTool, Description("List all apps in App Store Connect")]
    public async Task<string> ListApps(CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync("/apps", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get details for a specific app by bundle ID")]
    public async Task<string> GetAppByBundleId(
        [Description("The bundle ID (e.g., 'com.example.app')")] string bundleId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/apps?filter[bundleId]={bundleId}", cancellationToken);
        return FormatResponse(result);
    }

    // ==================== App Store Versions ====================

    [McpServerTool, Description("List all App Store versions for an app")]
    public async Task<string> ListAppStoreVersions(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/apps/{appId}/appStoreVersions", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get the latest editable App Store version for an app")]
    public async Task<string> GetEditableVersion(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        // Get versions that are editable (PREPARE_FOR_SUBMISSION, DEVELOPER_REJECTED, etc.)
        var result = await _client.GetAsync(
            $"/apps/{appId}/appStoreVersions?filter[appStoreState]=PREPARE_FOR_SUBMISSION,DEVELOPER_REJECTED,REJECTED,METADATA_REJECTED,WAITING_FOR_REVIEW,INVALID_BINARY",
            cancellationToken);
        return FormatResponse(result);
    }

    // ==================== App Store Version Localizations ====================

    [McpServerTool, Description("List localizations for an App Store version (metadata per language)")]
    public async Task<string> ListVersionLocalizations(
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/appStoreVersions/{versionId}/appStoreVersionLocalizations", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Update App Store version localization metadata (description, keywords, URLs, etc.)")]
    public async Task<string> UpdateVersionLocalization(
        [Description("The localization ID")] string localizationId,
        [Description("App description (max 4000 chars)")] string? description = null,
        [Description("Keywords for search (max 100 chars, comma-separated)")] string? keywords = null,
        [Description("Marketing URL")] string? marketingUrl = null,
        [Description("Support URL")] string? supportUrl = null,
        [Description("Promotional text (max 170 chars)")] string? promotionalText = null,
        [Description("What's new in this version")] string? whatsNew = null,
        CancellationToken cancellationToken = default)
    {
        var attributes = new Dictionary<string, object?>();

        if (description != null) attributes["description"] = description;
        if (keywords != null) attributes["keywords"] = keywords;
        if (marketingUrl != null) attributes["marketingUrl"] = marketingUrl;
        if (supportUrl != null) attributes["supportUrl"] = supportUrl;
        if (promotionalText != null) attributes["promotionalText"] = promotionalText;
        if (whatsNew != null) attributes["whatsNew"] = whatsNew;

        if (attributes.Count == 0)
        {
            return "Error: At least one field must be provided to update.";
        }

        var payload = new
        {
            data = new
            {
                type = "appStoreVersionLocalizations",
                id = localizationId,
                attributes = attributes
            }
        };

        var result = await _client.PatchAsync($"/appStoreVersionLocalizations/{localizationId}", payload, cancellationToken);
        return FormatResponse(result);
    }

    // ==================== Screenshot Sets ====================

    [McpServerTool, Description("List screenshot sets for a version localization")]
    public async Task<string> ListScreenshotSets(
        [Description("The localization ID")] string localizationId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/appStoreVersionLocalizations/{localizationId}/appScreenshotSets", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Create a screenshot set for a specific display type")]
    public async Task<string> CreateScreenshotSet(
        [Description("The localization ID")] string localizationId,
        [Description("Display type: APP_IPHONE_67, APP_IPHONE_65, APP_IPHONE_55, APP_IPAD_PRO_129, APP_IPAD_PRO_3GEN_129, etc.")] string screenshotDisplayType,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            data = new
            {
                type = "appScreenshotSets",
                attributes = new
                {
                    screenshotDisplayType = screenshotDisplayType
                },
                relationships = new
                {
                    appStoreVersionLocalization = new
                    {
                        data = new
                        {
                            type = "appStoreVersionLocalizations",
                            id = localizationId
                        }
                    }
                }
            }
        };

        var result = await _client.PostAsync("/appScreenshotSets", payload, cancellationToken);
        return FormatResponse(result);
    }

    // ==================== Screenshots ====================

    [McpServerTool, Description("List screenshots in a screenshot set")]
    public async Task<string> ListScreenshots(
        [Description("The screenshot set ID")] string screenshotSetId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/appScreenshotSets/{screenshotSetId}/appScreenshots", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Delete a screenshot by its ID")]
    public async Task<string> DeleteScreenshot(
        [Description("The screenshot ID to delete")] string screenshotId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.DeleteAsync($"/appScreenshots/{screenshotId}", cancellationToken);
            return $"Screenshot {screenshotId} deleted successfully.";
        }
        catch (Exception ex)
        {
            return $"Error deleting screenshot {screenshotId}: {ex.Message}";
        }
    }

    [McpServerTool, Description("Upload a screenshot from a local file path. Returns the screenshot ID when complete.")]
    public async Task<string> UploadScreenshot(
        [Description("The screenshot set ID")] string screenshotSetId,
        [Description("Local file path to the PNG screenshot")] string filePath,
        CancellationToken cancellationToken = default)
    {
        // Validate file exists
        if (!File.Exists(filePath))
        {
            return $"Error: File not found: {filePath}";
        }

        // Read file
        var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
        var fileName = Path.GetFileName(filePath);
        var fileSize = fileBytes.Length;

        // Calculate checksum
        using var md5 = MD5.Create();
        var hashBytes = md5.ComputeHash(fileBytes);
        var checksum = Convert.ToBase64String(hashBytes);

        // Step 1: Reserve the screenshot upload
        var reservePayload = new
        {
            data = new
            {
                type = "appScreenshots",
                attributes = new
                {
                    fileName = fileName,
                    fileSize = fileSize
                },
                relationships = new
                {
                    appScreenshotSet = new
                    {
                        data = new
                        {
                            type = "appScreenshotSets",
                            id = screenshotSetId
                        }
                    }
                }
            }
        };

        var reserveResult = await _client.PostAsync("/appScreenshots", reservePayload, cancellationToken);

        // Extract screenshot ID and upload operations
        var screenshotId = reserveResult.RootElement
            .GetProperty("data")
            .GetProperty("id")
            .GetString();

        var uploadOperations = reserveResult.RootElement
            .GetProperty("data")
            .GetProperty("attributes")
            .GetProperty("uploadOperations");

        // Step 2: Upload the binary parts
        foreach (var operation in uploadOperations.EnumerateArray())
        {
            var method = operation.GetProperty("method").GetString();
            var url = operation.GetProperty("url").GetString();
            var offset = operation.GetProperty("offset").GetInt32();
            var length = operation.GetProperty("length").GetInt32();

            if (method == "PUT" && url != null)
            {
                var chunk = new byte[length];
                Array.Copy(fileBytes, offset, chunk, 0, length);
                await _client.UploadBinaryAsync(url, chunk, "image/png", cancellationToken);
            }
        }

        // Step 3: Commit the upload
        var commitPayload = new
        {
            data = new
            {
                type = "appScreenshots",
                id = screenshotId,
                attributes = new
                {
                    uploaded = true,
                    sourceFileChecksum = checksum
                }
            }
        };

        var commitResult = await _client.PatchAsync($"/appScreenshots/{screenshotId}", commitPayload, cancellationToken);

        return $"Screenshot uploaded successfully!\nScreenshot ID: {screenshotId}\n\n{FormatResponse(commitResult)}";
    }

    [McpServerTool, Description("Upload all screenshots from a directory to a screenshot set")]
    public async Task<string> UploadScreenshotsFromDirectory(
        [Description("The screenshot set ID")] string screenshotSetId,
        [Description("Directory path containing PNG screenshots")] string directoryPath,
        CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(directoryPath))
        {
            return $"Error: Directory not found: {directoryPath}";
        }

        var pngFiles = Directory.GetFiles(directoryPath, "*.png").OrderBy(f => f).ToArray();
        if (pngFiles.Length == 0)
        {
            return $"Error: No PNG files found in {directoryPath}";
        }

        var results = new List<string>();
        foreach (var file in pngFiles)
        {
            try
            {
                var result = await UploadScreenshot(screenshotSetId, file, cancellationToken);
                results.Add($"✓ {Path.GetFileName(file)}: Success");
            }
            catch (Exception ex)
            {
                results.Add($"✗ {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return $"Uploaded {pngFiles.Length} screenshots:\n" + string.Join("\n", results);
    }

    // ==================== Helper: Full Screenshot Upload Flow ====================

    [McpServerTool, Description("Upload screenshots for an app - handles the full flow from app to screenshot upload")]
    public async Task<string> UploadAppScreenshots(
        [Description("The bundle ID (e.g., 'no.invotek.RewardE')")] string bundleId,
        [Description("Display type: APP_IPHONE_67, APP_IPAD_PRO_129, etc.")] string displayType,
        [Description("Directory path containing PNG screenshots")] string directoryPath,
        [Description("Locale (e.g., 'en-US')")] string locale = "en-US",
        CancellationToken cancellationToken = default)
    {
        var steps = new List<string>();

        try
        {
            // Step 1: Get app by bundle ID
            steps.Add("Step 1: Finding app...");
            var appResult = await _client.GetAsync($"/apps?filter[bundleId]={bundleId}", cancellationToken);
            var apps = appResult.RootElement.GetProperty("data");

            if (apps.GetArrayLength() == 0)
            {
                return $"Error: No app found with bundle ID: {bundleId}";
            }

            var appId = apps[0].GetProperty("id").GetString();
            steps.Add($"  Found app ID: {appId}");

            // Step 2: Get editable version
            steps.Add("Step 2: Finding editable App Store version...");
            var versionsResult = await _client.GetAsync(
                $"/apps/{appId}/appStoreVersions?filter[appStoreState]=PREPARE_FOR_SUBMISSION,DEVELOPER_REJECTED,REJECTED,METADATA_REJECTED",
                cancellationToken);
            var versions = versionsResult.RootElement.GetProperty("data");

            if (versions.GetArrayLength() == 0)
            {
                return string.Join("\n", steps) + "\nError: No editable App Store version found. Create a new version in App Store Connect first.";
            }

            var versionId = versions[0].GetProperty("id").GetString();
            var versionString = versions[0].GetProperty("attributes").GetProperty("versionString").GetString();
            steps.Add($"  Found version: {versionString} (ID: {versionId})");

            // Step 3: Get or create localization
            steps.Add($"Step 3: Finding {locale} localization...");
            var localizationsResult = await _client.GetAsync(
                $"/appStoreVersions/{versionId}/appStoreVersionLocalizations",
                cancellationToken);
            var localizations = localizationsResult.RootElement.GetProperty("data");

            string? localizationId = null;
            foreach (var loc in localizations.EnumerateArray())
            {
                var locLocale = loc.GetProperty("attributes").GetProperty("locale").GetString();
                if (locLocale == locale)
                {
                    localizationId = loc.GetProperty("id").GetString();
                    break;
                }
            }

            if (localizationId == null)
            {
                return string.Join("\n", steps) + $"\nError: No localization found for locale: {locale}";
            }
            steps.Add($"  Found localization ID: {localizationId}");

            // Step 4: Get or create screenshot set
            steps.Add($"Step 4: Finding/creating {displayType} screenshot set...");
            var setsResult = await _client.GetAsync(
                $"/appStoreVersionLocalizations/{localizationId}/appScreenshotSets",
                cancellationToken);
            var sets = setsResult.RootElement.GetProperty("data");

            string? screenshotSetId = null;
            foreach (var set in sets.EnumerateArray())
            {
                var setDisplayType = set.GetProperty("attributes").GetProperty("screenshotDisplayType").GetString();
                if (setDisplayType == displayType)
                {
                    screenshotSetId = set.GetProperty("id").GetString();
                    break;
                }
            }

            if (screenshotSetId == null)
            {
                // Create new screenshot set
                var createSetPayload = new
                {
                    data = new
                    {
                        type = "appScreenshotSets",
                        attributes = new
                        {
                            screenshotDisplayType = displayType
                        },
                        relationships = new
                        {
                            appStoreVersionLocalization = new
                            {
                                data = new
                                {
                                    type = "appStoreVersionLocalizations",
                                    id = localizationId
                                }
                            }
                        }
                    }
                };

                var createSetResult = await _client.PostAsync("/appScreenshotSets", createSetPayload, cancellationToken);
                screenshotSetId = createSetResult.RootElement.GetProperty("data").GetProperty("id").GetString();
                steps.Add($"  Created new screenshot set ID: {screenshotSetId}");
            }
            else
            {
                steps.Add($"  Found existing screenshot set ID: {screenshotSetId}");
            }

            // Step 5: Upload screenshots
            steps.Add("Step 5: Uploading screenshots...");
            var uploadResult = await UploadScreenshotsFromDirectory(screenshotSetId!, directoryPath, cancellationToken);
            steps.Add(uploadResult);

            return string.Join("\n", steps);
        }
        catch (Exception ex)
        {
            return string.Join("\n", steps) + $"\n\nError: {ex.Message}";
        }
    }

    // ==================== Builds ====================

    [McpServerTool, Description("List builds for an app that can be attached to an App Store version")]
    public async Task<string> ListAppBuilds(
        [Description("The app ID")] string appId,
        [Description("Filter by processing state (PROCESSING, FAILED, INVALID, VALID)")] string? processingState = "VALID",
        [Description("Maximum number of builds to return")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var endpoint = $"/apps/{appId}/builds?limit={limit}&sort=-uploadedDate";
        if (!string.IsNullOrEmpty(processingState))
        {
            endpoint += $"&filter[processingState]={processingState}";
        }

        var result = await _client.GetAsync(endpoint, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get a specific build by ID")]
    public async Task<string> GetBuild(
        [Description("The build ID")] string buildId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/builds/{buildId}", cancellationToken);
        return FormatResponse(result);
    }

    // ==================== App Store Version Management ====================

    [McpServerTool, Description("Attach a build to an App Store version")]
    public async Task<string> AttachBuildToVersion(
        [Description("The App Store version ID")] string versionId,
        [Description("The build ID to attach")] string buildId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            data = new
            {
                type = "builds",
                id = buildId
            }
        };

        try
        {
            var result = await _client.PatchAsync($"/appStoreVersions/{versionId}/relationships/build", payload, cancellationToken);
            return $"Build {buildId} attached to version {versionId} successfully.\n\n{FormatResponse(result)}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error attaching build: {ex.Message}";
        }
    }

    [McpServerTool, Description("Update App Store version attributes (copyright, release type, etc.)")]
    public async Task<string> UpdateAppStoreVersion(
        [Description("The App Store version ID")] string versionId,
        [Description("Copyright notice (e.g., '2025 Company Name')")] string? copyright = null,
        [Description("Release type: MANUAL, AFTER_APPROVAL, SCHEDULED")] string? releaseType = null,
        [Description("Earliest release date (ISO 8601 format, only for SCHEDULED)")] string? earliestReleaseDate = null,
        [Description("Whether the app uses IDFA (true/false)")] bool? usesIdfa = null,
        CancellationToken cancellationToken = default)
    {
        var attributes = new Dictionary<string, object?>();

        if (copyright != null) attributes["copyright"] = copyright;
        if (releaseType != null) attributes["releaseType"] = releaseType;
        if (earliestReleaseDate != null) attributes["earliestReleaseDate"] = earliestReleaseDate;
        if (usesIdfa.HasValue) attributes["usesIdfa"] = usesIdfa.Value;

        if (attributes.Count == 0)
        {
            return "Error: At least one field must be provided to update.";
        }

        var payload = new
        {
            data = new
            {
                type = "appStoreVersions",
                id = versionId,
                attributes = attributes
            }
        };

        var result = await _client.PatchAsync($"/appStoreVersions/{versionId}", payload, cancellationToken);
        return FormatResponse(result);
    }

    // ==================== Age Rating Declaration ====================

    [McpServerTool, Description("Get the age rating declaration for an App Store version")]
    public async Task<string> GetAgeRatingDeclaration(
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetAsync($"/appStoreVersions/{versionId}/ageRatingDeclaration", cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Update the age rating declaration for an app. All values should be: NONE, INFREQUENT_OR_MILD, FREQUENT_OR_INTENSE")]
    public async Task<string> UpdateAgeRatingDeclaration(
        [Description("The age rating declaration ID")] string declarationId,
        [Description("Alcohol, tobacco, or drug use references")] string? alcoholTobaccoOrDrugUseOrReferences = null,
        [Description("Contests")] string? contests = null,
        [Description("Gambling and contests (true/false for simulated gambling)")] bool? gamblingAndContests = null,
        [Description("Gambling simulated")] string? gamblingSimulated = null,
        [Description("Horror or fear themes")] string? horrorOrFearThemes = null,
        [Description("Mature or suggestive themes")] string? matureOrSuggestiveThemes = null,
        [Description("Medical or treatment information")] string? medicalOrTreatmentInformation = null,
        [Description("Profanity or crude humor")] string? profanityOrCrudeHumor = null,
        [Description("Sexual content or nudity")] string? sexualContentOrNudity = null,
        [Description("Sexual content or graphic nudity")] string? sexualContentGraphicAndNudity = null,
        [Description("Violence in cartoon or fantasy")] string? violenceCartoonOrFantasy = null,
        [Description("Violence realistic")] string? violenceRealistic = null,
        [Description("Violence realistic prolonged or graphic")] string? violenceRealisticProlongedGraphicOrSadistic = null,
        [Description("Unrestricted web access (true/false)")] bool? unrestrictedWebAccess = null,
        [Description("Koreas age rating override")] string? koreaAgeRatingOverride = null,
        [Description("17+ age gate required (true/false)")] bool? seventeenPlus = null,
        CancellationToken cancellationToken = default)
    {
        var attributes = new Dictionary<string, object?>();

        if (alcoholTobaccoOrDrugUseOrReferences != null) attributes["alcoholTobaccoOrDrugUseOrReferences"] = alcoholTobaccoOrDrugUseOrReferences;
        if (contests != null) attributes["contests"] = contests;
        if (gamblingAndContests.HasValue) attributes["gamblingAndContests"] = gamblingAndContests.Value;
        if (gamblingSimulated != null) attributes["gamblingSimulated"] = gamblingSimulated;
        if (horrorOrFearThemes != null) attributes["horrorOrFearThemes"] = horrorOrFearThemes;
        if (matureOrSuggestiveThemes != null) attributes["matureOrSuggestiveThemes"] = matureOrSuggestiveThemes;
        if (medicalOrTreatmentInformation != null) attributes["medicalOrTreatmentInformation"] = medicalOrTreatmentInformation;
        if (profanityOrCrudeHumor != null) attributes["profanityOrCrudeHumor"] = profanityOrCrudeHumor;
        if (sexualContentOrNudity != null) attributes["sexualContentOrNudity"] = sexualContentOrNudity;
        if (sexualContentGraphicAndNudity != null) attributes["sexualContentGraphicAndNudity"] = sexualContentGraphicAndNudity;
        if (violenceCartoonOrFantasy != null) attributes["violenceCartoonOrFantasy"] = violenceCartoonOrFantasy;
        if (violenceRealistic != null) attributes["violenceRealistic"] = violenceRealistic;
        if (violenceRealisticProlongedGraphicOrSadistic != null) attributes["violenceRealisticProlongedGraphicOrSadistic"] = violenceRealisticProlongedGraphicOrSadistic;
        if (unrestrictedWebAccess.HasValue) attributes["unrestrictedWebAccess"] = unrestrictedWebAccess.Value;
        if (koreaAgeRatingOverride != null) attributes["koreaAgeRatingOverride"] = koreaAgeRatingOverride;
        if (seventeenPlus.HasValue) attributes["seventeenPlus"] = seventeenPlus.Value;

        if (attributes.Count == 0)
        {
            return "Error: At least one field must be provided to update.";
        }

        var payload = new
        {
            data = new
            {
                type = "ageRatingDeclarations",
                id = declarationId,
                attributes = attributes
            }
        };

        var result = await _client.PatchAsync($"/ageRatingDeclarations/{declarationId}", payload, cancellationToken);
        return FormatResponse(result);
    }

    // ==================== App Store Review Details ====================

    [McpServerTool, Description("Get the App Store review detail for a version")]
    public async Task<string> GetAppStoreReviewDetail(
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetAsync($"/appStoreVersions/{versionId}/appStoreReviewDetail", cancellationToken);
            return FormatResponse(result);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("404"))
        {
            return "No review detail exists yet. Use CreateAppStoreReviewDetail to create one.";
        }
    }

    [McpServerTool, Description("Create App Store review details for a version")]
    public async Task<string> CreateAppStoreReviewDetail(
        [Description("The App Store version ID")] string versionId,
        [Description("Contact first name for review team")] string? contactFirstName = null,
        [Description("Contact last name for review team")] string? contactLastName = null,
        [Description("Contact phone number for review team")] string? contactPhone = null,
        [Description("Contact email for review team")] string? contactEmail = null,
        [Description("Demo account username (if login required)")] string? demoAccountName = null,
        [Description("Demo account password (if login required)")] string? demoAccountPassword = null,
        [Description("Whether demo account is required")] bool? demoAccountRequired = null,
        [Description("Notes for the App Review team")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var attributes = new Dictionary<string, object?>();

        if (contactFirstName != null) attributes["contactFirstName"] = contactFirstName;
        if (contactLastName != null) attributes["contactLastName"] = contactLastName;
        if (contactPhone != null) attributes["contactPhone"] = contactPhone;
        if (contactEmail != null) attributes["contactEmail"] = contactEmail;
        if (demoAccountName != null) attributes["demoAccountName"] = demoAccountName;
        if (demoAccountPassword != null) attributes["demoAccountPassword"] = demoAccountPassword;
        if (demoAccountRequired.HasValue) attributes["demoAccountRequired"] = demoAccountRequired.Value;
        if (notes != null) attributes["notes"] = notes;

        var payload = new
        {
            data = new
            {
                type = "appStoreReviewDetails",
                attributes = attributes,
                relationships = new
                {
                    appStoreVersion = new
                    {
                        data = new
                        {
                            type = "appStoreVersions",
                            id = versionId
                        }
                    }
                }
            }
        };

        var result = await _client.PostAsync("/appStoreReviewDetails", payload, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Update App Store review details")]
    public async Task<string> UpdateAppStoreReviewDetail(
        [Description("The review detail ID")] string reviewDetailId,
        [Description("Contact first name for review team")] string? contactFirstName = null,
        [Description("Contact last name for review team")] string? contactLastName = null,
        [Description("Contact phone number for review team")] string? contactPhone = null,
        [Description("Contact email for review team")] string? contactEmail = null,
        [Description("Demo account username (if login required)")] string? demoAccountName = null,
        [Description("Demo account password (if login required)")] string? demoAccountPassword = null,
        [Description("Whether demo account is required")] bool? demoAccountRequired = null,
        [Description("Notes for the App Review team")] string? notes = null,
        CancellationToken cancellationToken = default)
    {
        var attributes = new Dictionary<string, object?>();

        if (contactFirstName != null) attributes["contactFirstName"] = contactFirstName;
        if (contactLastName != null) attributes["contactLastName"] = contactLastName;
        if (contactPhone != null) attributes["contactPhone"] = contactPhone;
        if (contactEmail != null) attributes["contactEmail"] = contactEmail;
        if (demoAccountName != null) attributes["demoAccountName"] = demoAccountName;
        if (demoAccountPassword != null) attributes["demoAccountPassword"] = demoAccountPassword;
        if (demoAccountRequired.HasValue) attributes["demoAccountRequired"] = demoAccountRequired.Value;
        if (notes != null) attributes["notes"] = notes;

        if (attributes.Count == 0)
        {
            return "Error: At least one field must be provided to update.";
        }

        var payload = new
        {
            data = new
            {
                type = "appStoreReviewDetails",
                id = reviewDetailId,
                attributes = attributes
            }
        };

        var result = await _client.PatchAsync($"/appStoreReviewDetails/{reviewDetailId}", payload, cancellationToken);
        return FormatResponse(result);
    }

    // ==================== App Store Submission ====================

    [McpServerTool, Description("Submit an App Store version for review")]
    public async Task<string> SubmitForReview(
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        var payload = new
        {
            data = new
            {
                type = "appStoreVersionSubmissions",
                relationships = new
                {
                    appStoreVersion = new
                    {
                        data = new
                        {
                            type = "appStoreVersions",
                            id = versionId
                        }
                    }
                }
            }
        };

        try
        {
            var result = await _client.PostAsync("/appStoreVersionSubmissions", payload, cancellationToken);
            return $"App submitted for review successfully!\n\n{FormatResponse(result)}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error submitting for review: {ex.Message}\n\nMake sure all required fields are filled:\n- Build attached\n- Screenshots uploaded\n- App description\n- Age rating completed\n- Review information";
        }
    }

    [McpServerTool, Description("Check if an App Store version is ready for submission")]
    public async Task<string> CheckSubmissionReadiness(
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();
        var ready = new List<string>();

        try
        {
            // Check version details
            var versionResult = await _client.GetAsync($"/appStoreVersions/{versionId}", cancellationToken);
            var version = versionResult.RootElement.GetProperty("data");
            var attributes = version.GetProperty("attributes");

            // Check copyright
            if (attributes.TryGetProperty("copyright", out var copyright) && copyright.ValueKind != JsonValueKind.Null)
            {
                ready.Add($"✓ Copyright: {copyright.GetString()}");
            }
            else
            {
                issues.Add("✗ Copyright not set");
            }

            // Check build
            var buildResult = await _client.GetAsync($"/appStoreVersions/{versionId}/build", cancellationToken);
            if (buildResult.RootElement.TryGetProperty("data", out var buildData) && buildData.ValueKind != JsonValueKind.Null)
            {
                var buildVersion = buildData.GetProperty("attributes").GetProperty("version").GetString();
                ready.Add($"✓ Build attached: {buildVersion}");
            }
            else
            {
                issues.Add("✗ No build attached");
            }

            // Check localizations for description
            var locResult = await _client.GetAsync($"/appStoreVersions/{versionId}/appStoreVersionLocalizations", cancellationToken);
            var localizations = locResult.RootElement.GetProperty("data");
            var hasDescription = false;
            var hasScreenshots = false;

            foreach (var loc in localizations.EnumerateArray())
            {
                var locAttrs = loc.GetProperty("attributes");
                if (locAttrs.TryGetProperty("description", out var desc) && desc.ValueKind != JsonValueKind.Null && !string.IsNullOrEmpty(desc.GetString()))
                {
                    hasDescription = true;
                }

                // Check screenshots
                var locId = loc.GetProperty("id").GetString();
                var screenshotSets = await _client.GetAsync($"/appStoreVersionLocalizations/{locId}/appScreenshotSets", cancellationToken);
                var sets = screenshotSets.RootElement.GetProperty("data");
                if (sets.GetArrayLength() > 0)
                {
                    hasScreenshots = true;
                }
            }

            if (hasDescription) ready.Add("✓ Description provided");
            else issues.Add("✗ Description missing");

            if (hasScreenshots) ready.Add("✓ Screenshots uploaded");
            else issues.Add("✗ Screenshots missing");

            // Check age rating
            try
            {
                var ageResult = await _client.GetAsync($"/appStoreVersions/{versionId}/ageRatingDeclaration", cancellationToken);
                ready.Add("✓ Age rating declaration exists");
            }
            catch
            {
                issues.Add("✗ Age rating not configured");
            }

            // Build result
            var result = new System.Text.StringBuilder();
            result.AppendLine("=== Submission Readiness Check ===\n");

            if (ready.Count > 0)
            {
                result.AppendLine("Ready:");
                foreach (var item in ready) result.AppendLine($"  {item}");
            }

            if (issues.Count > 0)
            {
                result.AppendLine("\nNeeds attention:");
                foreach (var issue in issues) result.AppendLine($"  {issue}");
                result.AppendLine($"\n⚠️  {issues.Count} issue(s) found. Fix before submitting.");
            }
            else
            {
                result.AppendLine("\n✅ Ready to submit for review!");
            }

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Error checking readiness: {ex.Message}";
        }
    }

    // ==================== App Pricing ====================

    [McpServerTool, Description("Set app pricing to Free. Creates/updates the app price schedule with a $0 price point.")]
    public async Task<string> SetAppPricingFree(
        [Description("The app ID")] string appId,
        [Description("Base territory (default: USA)")] string baseTerritory = "USA",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Get the FREE price point for the territory
            var pricePointsResult = await _client.GetAsync(
                $"/apps/{appId}/appPricePoints?filter[territory]={baseTerritory}&limit=200",
                cancellationToken);

            string? freePricePointId = null;
            foreach (var pp in pricePointsResult.RootElement.GetProperty("data").EnumerateArray())
            {
                var customerPrice = pp.GetProperty("attributes").GetProperty("customerPrice").GetString();
                if (customerPrice == "0" || customerPrice == "0.0" || customerPrice == "0.00")
                {
                    freePricePointId = pp.GetProperty("id").GetString();
                    break;
                }
            }

            if (freePricePointId == null)
            {
                return "Error: Could not find FREE price point for territory " + baseTerritory;
            }

            // Step 2: Create price schedule with the free price point
            var payload = new
            {
                data = new
                {
                    type = "appPriceSchedules",
                    relationships = new
                    {
                        app = new
                        {
                            data = new { type = "apps", id = appId }
                        },
                        baseTerritory = new
                        {
                            data = new { type = "territories", id = baseTerritory }
                        },
                        manualPrices = new
                        {
                            data = new[]
                            {
                                new { type = "appPrices", id = "${price1}" }
                            }
                        }
                    }
                },
                included = new object[]
                {
                    new
                    {
                        type = "appPrices",
                        id = "${price1}",
                        attributes = new
                        {
                            startDate = (string?)null
                        },
                        relationships = new
                        {
                            appPricePoint = new
                            {
                                data = new { type = "appPricePoints", id = freePricePointId }
                            }
                        }
                    }
                }
            };

            var result = await _client.PostAsync("/appPriceSchedules", payload, cancellationToken);
            return $"App pricing set to FREE successfully!\n\n{FormatResponse(result)}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error setting pricing: {ex.Message}";
        }
    }

    [McpServerTool, Description("Get current app pricing schedule")]
    public async Task<string> GetAppPricing(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetAsync($"/apps/{appId}/appPriceSchedule", cancellationToken);
            return FormatResponse(result);
        }
        catch (HttpRequestException ex)
        {
            return $"Error getting pricing: {ex.Message}";
        }
    }

    // ==================== App Data Privacy ====================
    // NOTE: The App Store Connect API does NOT expose app privacy/data usage endpoints publicly.
    // Privacy labels must be configured manually in App Store Connect web interface.

    [McpServerTool, Description("Get app's data privacy/usage declarations")]
    public async Task<string> GetAppDataUsages(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        // The appDataUsages endpoint is not available in the public API
        return $@"**App Privacy Labels - Manual Step Required**

The App Store Connect API does not expose app privacy/data usage endpoints publicly.
You must configure privacy labels manually in App Store Connect:

1. Go to: https://appstoreconnect.apple.com/apps/{appId}/appstore/appprivacy
2. Answer the questionnaire about data collection
3. Click 'Publish' when done

For apps that don't collect data:
- Select 'No' for all data collection questions
- Click 'Publish' to confirm

This is a one-time setup per app.";
    }

    [McpServerTool, Description("Declare that app does not collect any user data and publish the privacy declaration")]
    public async Task<string> SetAppDataPrivacyNoCollection(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        // The appDataUsages endpoint is not available in the public API
        return $@"**App Privacy Labels - Manual Step Required**

The App Store Connect API does not support setting privacy labels programmatically.
You must configure this manually in App Store Connect:

1. Go to: https://appstoreconnect.apple.com/apps/{appId}/appstore/appprivacy
2. For each data type question, select 'No, we don't collect this data'
3. When all questions are answered, click 'Publish'

This confirms your app doesn't collect user data and publishes the privacy label.

After publishing, run SubmitForReviewV2 again to check if all blockers are resolved.";
    }

    // ==================== Review Submission (V2 API) ====================

    [McpServerTool, Description("Submit app for review using the new reviewSubmissions API (provides detailed error messages)")]
    public async Task<string> SubmitForReviewV2(
        [Description("The app ID")] string appId,
        [Description("The App Store version ID")] string versionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Step 1: Create review submission
            var createPayload = new
            {
                data = new
                {
                    type = "reviewSubmissions",
                    relationships = new
                    {
                        app = new
                        {
                            data = new { type = "apps", id = appId }
                        }
                    }
                }
            };

            var createResult = await _client.PostAsync("/reviewSubmissions", createPayload, cancellationToken);
            var submissionId = createResult.RootElement
                .GetProperty("data")
                .GetProperty("id")
                .GetString();

            // Step 2: Add the version to the submission
            var addItemPayload = new
            {
                data = new
                {
                    type = "reviewSubmissionItems",
                    relationships = new
                    {
                        reviewSubmission = new
                        {
                            data = new { type = "reviewSubmissions", id = submissionId }
                        },
                        appStoreVersion = new
                        {
                            data = new { type = "appStoreVersions", id = versionId }
                        }
                    }
                }
            };

            await _client.PostAsync("/reviewSubmissionItems", addItemPayload, cancellationToken);

            // Step 3: Submit for review
            var submitPayload = new
            {
                data = new
                {
                    type = "reviewSubmissions",
                    id = submissionId,
                    attributes = new
                    {
                        submitted = true
                    }
                }
            };

            var submitResult = await _client.PatchAsync($"/reviewSubmissions/{submissionId}", submitPayload, cancellationToken);

            return $"App submitted for review successfully!\nSubmission ID: {submissionId}\n\n{FormatResponse(submitResult)}";
        }
        catch (HttpRequestException ex)
        {
            // Parse the error to show detailed blocking issues
            var errorMessage = ex.Message;

            if (errorMessage.Contains("associatedErrors"))
            {
                return $"Submission blocked. Missing requirements:\n\n{errorMessage}";
            }

            return $"Error submitting for review: {errorMessage}";
        }
    }

    [McpServerTool, Description("Get existing review submissions for an app")]
    public async Task<string> GetReviewSubmissions(
        [Description("The app ID")] string appId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.GetAsync($"/apps/{appId}/reviewSubmissions", cancellationToken);
            return FormatResponse(result);
        }
        catch (HttpRequestException ex)
        {
            return $"Error getting review submissions: {ex.Message}";
        }
    }

    [McpServerTool, Description("Cancel a pending review submission")]
    public async Task<string> CancelReviewSubmission(
        [Description("The review submission ID")] string submissionId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new
            {
                data = new
                {
                    type = "reviewSubmissions",
                    id = submissionId,
                    attributes = new
                    {
                        canceled = true
                    }
                }
            };

            var result = await _client.PatchAsync($"/reviewSubmissions/{submissionId}", payload, cancellationToken);
            return $"Review submission canceled.\n\n{FormatResponse(result)}";
        }
        catch (HttpRequestException ex)
        {
            return $"Error canceling submission: {ex.Message}";
        }
    }

    private static string FormatResponse(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
