using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace AppStoreConnectMcp;

/// <summary>
/// MCP tools for accessing Xcode Cloud build information via App Store Connect API.
/// </summary>
[McpServerToolType]
public class XcodeCloudTools
{
    private readonly AppStoreConnectClient _client;

    public XcodeCloudTools(AppStoreConnectClient client)
    {
        _client = client;
    }

    [McpServerTool, Description("List all Xcode Cloud products (apps configured with Xcode Cloud)")]
    public async Task<string> ListProducts(CancellationToken cancellationToken = default)
    {
        var result = await _client.ListCiProductsAsync(cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("List recent builds for a specific Xcode Cloud product")]
    public async Task<string> ListBuilds(
        [Description("The product ID (from ListProducts)")] string productId,
        [Description("Maximum number of builds to return (default: 10)")] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ListBuildsForProductAsync(productId, limit, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get details for a specific Xcode Cloud build run")]
    public async Task<string> GetBuildRun(
        [Description("The build run ID")] string buildRunId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetBuildRunAsync(buildRunId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("List all actions for a specific build run (e.g., build, test, archive)")]
    public async Task<string> ListBuildActions(
        [Description("The build run ID")] string buildRunId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ListBuildActionsAsync(buildRunId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get details for a specific build action")]
    public async Task<string> GetBuildAction(
        [Description("The action ID (from ListBuildActions)")] string actionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetBuildActionAsync(actionId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("List artifacts (logs, archives) for a build action")]
    public async Task<string> ListArtifacts(
        [Description("The action ID")] string actionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ListArtifactsAsync(actionId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get test results for a build action")]
    public async Task<string> GetTestResults(
        [Description("The action ID")] string actionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetTestResultsAsync(actionId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("List issues (errors and warnings) for a build action - useful for debugging failed builds")]
    public async Task<string> ListIssues(
        [Description("The action ID")] string actionId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ListIssuesAsync(actionId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Download and parse build logs for a build action - returns detailed error information from Xcode build logs")]
    public async Task<string> GetBuildLogs(
        [Description("The action ID (from ListBuildActions)")] string actionId,
        [Description("Number of lines to return from end of each log file (default: 500)")] int tailLines = 500,
        CancellationToken cancellationToken = default)
    {
        return await _client.GetBuildLogsAsync(actionId, tailLines, cancellationToken);
    }

    // ==================== Workflow Management ====================

    [McpServerTool, Description("List all Xcode Cloud workflows for a product")]
    public async Task<string> ListWorkflows(
        [Description("The product ID (from ListProducts)")] string productId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.ListWorkflowsAsync(productId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Get details for a specific Xcode Cloud workflow including branch filters and triggers")]
    public async Task<string> GetWorkflow(
        [Description("The workflow ID (from ListWorkflows)")] string workflowId,
        CancellationToken cancellationToken = default)
    {
        var result = await _client.GetWorkflowAsync(workflowId, cancellationToken);
        return FormatResponse(result);
    }

    [McpServerTool, Description("Start a new Xcode Cloud build for a workflow")]
    public async Task<string> StartBuild(
        [Description("The workflow ID to trigger")] string workflowId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _client.StartBuildAsync(workflowId, null, cancellationToken);
            return FormatResponse(result);
        }
        catch (HttpRequestException ex)
        {
            return $"Failed to start build: {ex.Message}";
        }
    }

    [McpServerTool, Description("Cancel a running Xcode Cloud build")]
    public async Task<string> CancelBuild(
        [Description("The build run ID to cancel")] string buildRunId,
        CancellationToken cancellationToken = default)
    {
        // The App Store Connect API does not support cancelling builds programmatically.
        // The ciBuildRuns endpoint only allows CREATE and GET operations.
        return $@"**Cannot Cancel Build via API**

The App Store Connect API does not support cancelling Xcode Cloud builds programmatically.
The ciBuildRuns endpoint only allows CREATE (start) and GET (read) operations.

To cancel build {buildRunId} manually:
1. Open Xcode → Report Navigator → Cloud tab
2. Find the build and click 'Cancel'

Or use App Store Connect web:
1. Go to https://appstoreconnect.apple.com
2. Navigate to your app → Xcode Cloud → Builds
3. Find the build and cancel it";
    }

    [McpServerTool, Description("Check if an app is set up for Xcode Cloud and get setup instructions if not")]
    public async Task<string> CheckXcodeCloudSetup(
        [Description("The bundle ID to check (e.g., 'com.example.app')")] string bundleId,
        CancellationToken cancellationToken = default)
    {
        var result = new System.Text.StringBuilder();
        result.AppendLine($"## Xcode Cloud Setup Check: {bundleId}");
        result.AppendLine();

        // Step 1: Check if bundle ID exists
        try
        {
            var bundleIdsResult = await _client.GetAsync($"/bundleIds?filter[identifier]={bundleId}", cancellationToken);
            var bundleIds = bundleIdsResult.RootElement.GetProperty("data");

            if (bundleIds.GetArrayLength() == 0)
            {
                result.AppendLine("❌ **Bundle ID not registered**");
                result.AppendLine();
                result.AppendLine("Use the `RegisterBundleId` tool to register it:");
                result.AppendLine($"- identifier: `{bundleId}`");
                result.AppendLine("- name: `Your App Name`");
                result.AppendLine("- platform: `IOS`");
                return result.ToString();
            }
            result.AppendLine("✅ Bundle ID is registered");
        }
        catch
        {
            result.AppendLine("⚠️ Could not check bundle ID status");
        }

        // Step 2: Check if app exists in App Store Connect
        try
        {
            var appsResult = await _client.GetAsync($"/apps?filter[bundleId]={bundleId}", cancellationToken);
            var apps = appsResult.RootElement.GetProperty("data");

            if (apps.GetArrayLength() == 0)
            {
                result.AppendLine("❌ **App not registered in App Store Connect**");
                result.AppendLine();
                result.AppendLine("The App Store Connect API does **not** support creating apps programmatically.");
                result.AppendLine("You must create the app manually:");
                result.AppendLine();
                result.AppendLine("1. Go to https://appstoreconnect.apple.com");
                result.AppendLine("2. Click '+ New App' or 'My Apps' → '+'");
                result.AppendLine("3. Fill in:");
                result.AppendLine($"   - Bundle ID: Select `{bundleId}`");
                result.AppendLine("   - Name: Your app name");
                result.AppendLine("   - Primary Language: English (US)");
                result.AppendLine("   - SKU: Unique identifier (e.g., your-app-001)");
                result.AppendLine("4. Click 'Create'");
                return result.ToString();
            }

            var appId = apps[0].GetProperty("id").GetString();
            var appName = apps[0].GetProperty("attributes").GetProperty("name").GetString();
            result.AppendLine($"✅ App exists: {appName} (ID: {appId})");
        }
        catch
        {
            result.AppendLine("⚠️ Could not check app status");
        }

        // Step 3: Check if app has Xcode Cloud products
        try
        {
            var productsResult = await _client.ListCiProductsAsync(cancellationToken);
            var products = productsResult.RootElement.GetProperty("data");

            string? matchingProductId = null;
            foreach (var product in products.EnumerateArray())
            {
                var productBundleId = product.GetProperty("attributes").GetProperty("bundleId").GetString();
                if (productBundleId == bundleId)
                {
                    matchingProductId = product.GetProperty("id").GetString();
                    var productName = product.GetProperty("attributes").GetProperty("name").GetString();
                    result.AppendLine($"✅ Xcode Cloud product exists: {productName} (ID: {matchingProductId})");
                    break;
                }
            }

            if (matchingProductId == null)
            {
                result.AppendLine("❌ **Xcode Cloud not configured**");
                result.AppendLine();
                result.AppendLine("Xcode Cloud must be set up from Xcode:");
                result.AppendLine();
                result.AppendLine("1. Open your project in Xcode");
                result.AppendLine("2. Select Product → Xcode Cloud → Create Workflow");
                result.AppendLine("3. Connect your GitHub/GitLab repository");
                result.AppendLine("4. Configure your first workflow");
                result.AppendLine();
                result.AppendLine("After setup, use `ListProducts` to verify, then `ListWorkflows` to see workflows.");
                return result.ToString();
            }

            // Step 4: List workflows if product exists
            var workflowsResult = await _client.ListWorkflowsAsync(matchingProductId!, cancellationToken);
            var workflows = workflowsResult.RootElement.GetProperty("data");

            if (workflows.GetArrayLength() == 0)
            {
                result.AppendLine("⚠️ No workflows configured");
                result.AppendLine();
                result.AppendLine("Create a workflow in Xcode: Product → Xcode Cloud → Manage Workflows");
            }
            else
            {
                result.AppendLine($"✅ {workflows.GetArrayLength()} workflow(s) configured:");
                foreach (var workflow in workflows.EnumerateArray())
                {
                    var workflowName = workflow.GetProperty("attributes").GetProperty("name").GetString();
                    var workflowId = workflow.GetProperty("id").GetString();
                    result.AppendLine($"   - {workflowName} (ID: {workflowId})");
                }
                result.AppendLine();
                result.AppendLine("Use `StartBuild` with a workflow ID to trigger a build.");
            }
        }
        catch
        {
            result.AppendLine("⚠️ Could not check Xcode Cloud products");
        }

        result.AppendLine();
        result.AppendLine("---");
        result.AppendLine("**Setup Complete!** Your app is ready for Xcode Cloud.");

        return result.ToString();
    }

    private static string FormatResponse(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
