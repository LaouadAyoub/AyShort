using AyShort.McpServer.Tools;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport()
    .WithTools<ServerInfoTools>();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();
