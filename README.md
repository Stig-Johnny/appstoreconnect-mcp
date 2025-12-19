# App Store Connect MCP Server

A Model Context Protocol (MCP) server for accessing Apple's App Store Connect API, specifically for Xcode Cloud build information.

## Features

- List Xcode Cloud products (apps configured with Xcode Cloud)
- List and inspect build runs
- Get build actions (build, test, archive steps)
- View build artifacts and logs
- Get test results
- List build issues (errors and warnings) - useful for debugging failed builds

## Prerequisites

- .NET 8.0 SDK or later
- Apple Developer account with App Store Connect API access
- App Store Connect API key (.p8 file)

## Getting Your API Key

1. Go to [App Store Connect](https://appstoreconnect.apple.com)
2. Navigate to **Users and Access** → **Integrations** → **App Store Connect API**
3. Click **Generate API Key** (or use existing)
4. Download the `.p8` private key file (only available once!)
5. Note the **Key ID** and **Issuer ID**

Required permissions for Xcode Cloud access:
- App Manager or Admin role
- Access to the apps you want to monitor

## Installation

### Build from Source

```bash
git clone https://github.com/Stig-Johnny/appstoreconnect-mcp.git
cd appstoreconnect-mcp/AppStoreConnectMcp
dotnet build -c Release
```

### Download Release

Download the latest release from the [Releases page](https://github.com/Stig-Johnny/appstoreconnect-mcp/releases).

## Configuration

The server requires the following environment variables:

| Variable | Description |
|----------|-------------|
| `APP_STORE_CONNECT_KEY_ID` | Your API key ID (e.g., `ABC123DEF4`) |
| `APP_STORE_CONNECT_ISSUER_ID` | Your issuer ID (UUID format) |
| `APP_STORE_CONNECT_KEY_PATH` | Path to your .p8 private key file |
| `APP_STORE_CONNECT_KEY_CONTENT` | Alternative: The private key content directly |

You must provide either `KEY_PATH` or `KEY_CONTENT`, not both.

## Usage with Claude Code

Add to your `.mcp.json` configuration:

```json
{
  "mcpServers": {
    "appstoreconnect": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/appstoreconnect-mcp/AppStoreConnectMcp"],
      "env": {
        "APP_STORE_CONNECT_KEY_ID": "YOUR_KEY_ID",
        "APP_STORE_CONNECT_ISSUER_ID": "YOUR_ISSUER_ID",
        "APP_STORE_CONNECT_KEY_PATH": "/path/to/AuthKey_XXXX.p8"
      }
    }
  }
}
```

Or using the compiled executable:

```json
{
  "mcpServers": {
    "appstoreconnect": {
      "type": "stdio",
      "command": "/path/to/appstoreconnect-mcp/AppStoreConnectMcp/bin/Release/net8.0/AppStoreConnectMcp",
      "env": {
        "APP_STORE_CONNECT_KEY_ID": "YOUR_KEY_ID",
        "APP_STORE_CONNECT_ISSUER_ID": "YOUR_ISSUER_ID",
        "APP_STORE_CONNECT_KEY_PATH": "/path/to/AuthKey_XXXX.p8"
      }
    }
  }
}
```

## Available Tools

### ListProducts
List all Xcode Cloud products (apps configured with Xcode Cloud).

### ListBuilds
List recent builds for a specific product.
- `productId`: The product ID (from ListProducts)
- `limit`: Maximum builds to return (default: 10)

### GetBuildRun
Get details for a specific build run.
- `buildRunId`: The build run ID

### ListBuildActions
List all actions for a build run (build, test, archive steps).
- `buildRunId`: The build run ID

### GetBuildAction
Get details for a specific build action.
- `actionId`: The action ID

### ListArtifacts
List artifacts (logs, archives) for a build action.
- `actionId`: The action ID

### GetTestResults
Get test results for a build action.
- `actionId`: The action ID

### ListIssues
List issues (errors and warnings) for a build action. This is particularly useful for debugging failed builds.
- `actionId`: The action ID

## Example Workflow

1. **List products** to find your app:
   ```
   ListProducts
   ```

2. **List recent builds** for your product:
   ```
   ListBuilds(productId: "abc123", limit: 5)
   ```

3. **Get build actions** to find the failed step:
   ```
   ListBuildActions(buildRunId: "xyz789")
   ```

4. **List issues** to see errors:
   ```
   ListIssues(actionId: "action123")
   ```

## Security Notes

- Never commit your `.p8` private key to version control
- Use environment variables or secure secret management
- The API key grants access to your App Store Connect account
- Consider using the `KEY_CONTENT` variable for containerized deployments

## API Documentation

This server implements a subset of Apple's [App Store Connect API](https://developer.apple.com/documentation/appstoreconnectapi):

- [Xcode Cloud Workflows and Builds](https://developer.apple.com/documentation/appstoreconnectapi/xcode_cloud_workflows_and_builds)
- [ciBuildRuns](https://developer.apple.com/documentation/appstoreconnectapi/cibuildrun)
- [ciBuildActions](https://developer.apple.com/documentation/appstoreconnectapi/cibuildaction)

## License

MIT License - see [LICENSE](LICENSE) file.
