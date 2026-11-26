param([string]$Endpoint = 'http://127.0.0.1:5143/mcp')

$ErrorActionPreference = 'Stop'
$headers = @{ Accept = 'application/json, text/event-stream' }

function Send-McpRequest($Message) {
    $response = Invoke-WebRequest -Uri $Endpoint -Method Post -Headers $headers `
        -ContentType 'application/json' -Body ($Message | ConvertTo-Json -Depth 10 -Compress) `
        -TimeoutSec 15

    # Streamable HTTP permits either JSON or SSE responses. This checkpoint
    # returns one response message per request; no general SSE client is needed.
    if ($response.Headers['Content-Type'] -like '*text/event-stream*') {
        $data = @($response.Content -split '\r?\n' | Where-Object { $_.StartsWith('data: ') })
        if ($data.Count -ne 1) { throw 'Expected one SSE data message.' }
        return $data[0].Substring(6) | ConvertFrom-Json
    }
    return $response.Content | ConvertFrom-Json
}

$initialize = Send-McpRequest @{
    jsonrpc = '2.0'; id = 1; method = 'initialize'
    params = @{
        protocolVersion = '2025-11-25'; capabilities = @{}
        clientInfo = @{ name = 'AyShort-checkpoint'; version = '0.1.0' }
    }
}
if ($initialize.id -ne 1 -or !$initialize.result.capabilities.tools) {
    throw 'Initialization did not return tool capabilities.'
}
$headers['MCP-Protocol-Version'] = $initialize.result.protocolVersion

$null = Invoke-WebRequest -Uri $Endpoint -Method Post -Headers $headers `
    -ContentType 'application/json' -TimeoutSec 15 `
    -Body '{"jsonrpc":"2.0","method":"notifications/initialized"}'

$list = Send-McpRequest @{ jsonrpc = '2.0'; id = 2; method = 'tools/list'; params = @{} }
if ($list.id -ne 2 -or $list.result.tools.Count -ne 1 -or
    $list.result.tools[0].name -ne 'server_info' -or
    $list.result.tools[0].inputSchema.type -ne 'object' -or
    !$list.result.tools[0].annotations.readOnlyHint) {
    throw 'Expected exactly one read-only server_info tool with an object input schema.'
}

$call = Send-McpRequest @{
    jsonrpc = '2.0'; id = 3; method = 'tools/call'
    params = @{ name = 'server_info'; arguments = @{} }
}
if ($call.id -ne 3 -or $call.result.isError -or
    $call.result.content[0].text -notlike 'AyShort MCP server:*') {
    throw 'server_info did not return the expected successful result.'
}

$initialize, $list, $call | ConvertTo-Json -Depth 15
Write-Host 'PASS: initialize, tools/list and tools/call over Streamable HTTP.'
