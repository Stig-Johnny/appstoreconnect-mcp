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
        try
        {
            await _client.CancelBuildAsync(buildRunId, cancellationToken);
            return $"Build {buildRunId} cancelled successfully.";
        }
        catch (HttpRequestException ex)
        {
            return $"Failed to cancel build: {ex.Message}";
        }
    }

    private static string FormatResponse(JsonDocument doc)
    {
        return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
    }
}
