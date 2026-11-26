using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AyShort.McpServer.Tools;

[McpServerToolType]
public sealed class ServerInfoTools
{
    [McpServerTool(Name = "server_info", ReadOnly = true, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Returns information about this local AyShort MCP checkpoint. Does not access the API or change data.")]
    public static string GetServerInfo() =>
        "AyShort MCP server: local Streamable HTTP checkpoint. Link creation and statistics are not implemented yet.";
}
