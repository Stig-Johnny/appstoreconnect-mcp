using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol;
using AppStoreConnectMcp;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<AppStoreConnectClient>();
builder.Services.AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var app = builder.Build();
await app.RunAsync();
