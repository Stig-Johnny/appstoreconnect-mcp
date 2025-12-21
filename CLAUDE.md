# App Store Connect MCP - Development Guide

## Project Overview

MCP server for Apple's App Store Connect API, focused on Xcode Cloud build information.

**Tech Stack:** .NET 8, ModelContextProtocol.Core, ES256 JWT auth

## Development Workflow

### Local Development

```bash
cd AppStoreConnectMcp

# Build
dotnet build

# Run directly (for testing outside Claude Code)
APP_STORE_CONNECT_KEY_ID=xxx \
APP_STORE_CONNECT_ISSUER_ID=xxx \
APP_STORE_CONNECT_KEY_CONTENT="-----BEGIN PRIVATE KEY-----..." \
dotnet run
```

### Testing with Claude Code

Two options for testing changes:

**Option A: Debug build (quick iteration)**
1. Make changes
2. `dotnet build`
3. Update `.mcp.json` to point to `bin/Debug/net8.0/AppStoreConnectMcp`
4. Restart Claude Code
5. Test tools

**Option B: Published build (closer to release)**
1. Make changes
2. `dotnet publish -c Release -r osx-arm64 -o ~/.claude/mcp-servers/appstoreconnect-mcp`
3. Restart Claude Code
4. Test tools

### Release Process

1. Commit and push changes to `main`
2. Create and push a tag: `git tag v1.x.x && git push origin v1.x.x`
3. GitHub Actions builds for all platforms automatically
4. Release created with binaries attached

## Current Tools

| Tool | Description | Status |
|------|-------------|--------|
| ListProducts | List Xcode Cloud products | Done |
| ListBuilds | List builds for a product | Done |
| GetBuildRun | Get build run details | Done |
| ListBuildActions | List actions in a build | Done |
| GetBuildAction | Get action details | Done |
| ListArtifacts | List build artifacts | Done |
| GetTestResults | Get test results | Done |
| ListIssues | List build errors/warnings | Done |
| GetBuildLogs | Download & parse build logs | Done |

## Potential Enhancements

### High Priority
- [ ] **ListWorkflows** - List Xcode Cloud workflows for a product
- [ ] **GetWorkflow** - Get workflow details (branch filters, triggers)
- [ ] **StartBuild** - Trigger a new build for a workflow
- [ ] **CancelBuild** - Cancel a running build

### Medium Priority
- [ ] **ListApps** - List all apps (not just Xcode Cloud enabled)
- [ ] **GetApp** - Get app details (bundle ID, name, etc.)
- [ ] **ListBetaTesters** - List TestFlight testers

### Lower Priority
- [ ] **GetBuildMetrics** - Build duration, success rate trends
- [ ] **ListMacOSVersions** - Available macOS versions for builds
- [ ] **ListXcodeVersions** - Available Xcode versions

## API Reference

- [App Store Connect API](https://developer.apple.com/documentation/appstoreconnectapi)
- [Xcode Cloud Workflows and Builds](https://developer.apple.com/documentation/appstoreconnectapi/xcode_cloud_workflows_and_builds)
- [CI Products](https://developer.apple.com/documentation/appstoreconnectapi/ciproducts)
- [CI Build Runs](https://developer.apple.com/documentation/appstoreconnectapi/cibuildrun)

## Architecture

```
AppStoreConnectMcp/
├── Program.cs              # Entry point, MCP server setup
├── AppStoreConnectClient.cs # API client, JWT auth, HTTP calls
└── XcodeCloudTools.cs      # MCP tool definitions
```

### Adding a New Tool

1. Add API method to `AppStoreConnectClient.cs`:
```csharp
public async Task<JsonDocument> ListWorkflowsAsync(string productId, CancellationToken ct = default)
{
    return await GetAsync($"/ciProducts/{productId}/workflows", ct);
}
```

2. Add tool to `XcodeCloudTools.cs`:
```csharp
[McpServerTool, Description("List workflows for a product")]
public async Task<string> ListWorkflows(
    [Description("The product ID")] string productId,
    CancellationToken cancellationToken = default)
{
    var result = await _client.ListWorkflowsAsync(productId, cancellationToken);
    return FormatResponse(result);
}
```

3. Build, test, commit, tag for release.

## Testing Checklist

Before releasing:
- [ ] `dotnet build` succeeds
- [ ] Test ListProducts returns your apps
- [ ] Test ListBuilds returns recent builds
- [ ] Test ListIssues on a failed build shows errors
- [ ] Test with Claude Code (restart after build)

## Credentials

Stored in Bitwarden:
- `App Store Connect - Key ID`
- `App Store Connect - Issuer ID`
- `App Store Connect - Private Key`

Get from: App Store Connect → Users and Access → Integrations → App Store Connect API
