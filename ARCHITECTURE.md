# Mew Agent Architecture

## What This Is

A console AI agent that tests MCP (Model Context Protocol) integration with Microsoft Semantic Kernel. Built for testing how LLMs interact with external tools via MCP.

## Structure

```
MewAgent/               # Console app - the actual agent
├── Program.cs          # Entry point
├── Services/
│   ├── MewAgentService.cs      # Orchestrates Semantic Kernel
│   ├── McpClientService.cs     # MCP client that discovers tools
│   └── TimerService.cs         # Timer system for delayed actions
└── Plugins/
    └── TimerPlugin.cs          # Timer functions for LLM

McpServerRefrigerator/  # Mock MCP server for testing
├── Controllers/
│   ├── McpController.cs        # Legacy HTTP endpoints
│   └── McpBridgeController.cs  # MCP protocol bridge
└── Services/
    ├── RefrigeratorService.cs  # Mock refrigerator logic
    └── ToolExecutionService.cs # Routes tool calls
```

## How It Works

1. **MewAgent** starts, connects to MCP server via HTTP transport
2. **McpClientService** uses official MCP SDK to discover tools
3. Tools become Semantic Kernel functions the LLM can call
4. User types → LLM decides which tools to call → Results back to user

## Key Components

### MewAgentService
- Creates Semantic Kernel with your LLM config
- Manages conversation history
- Handles tool calling automatically via `ToolCallBehavior.AutoInvokeKernelFunctions`

### McpClientService  
- Simple HTTP client for tool discovery and execution
- Connects to HTTP-based MCP servers
- Discovers tools: GET /api/mcp/tools
- Executes tools: POST /api/mcp/execute
- Converts tools to Semantic Kernel functions

### TimerService
- Background timer that checks every 5 seconds
- Stores user context and replays to LLM when timer expires
- Used for: delayed responses, reminders, entertainment mode

## Configuration

`MewAgent/appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-key",
    "ModelId": "model-name",
    "Endpoint": "https://api.endpoint"  // Optional
  },
  "McpServer": {
    "HttpUrl": "http://localhost:5100"
  }
}
```

## Adding Tools

1. Add to `ToolExecutionService.GetAvailableTools()`
2. Implement in `RefrigeratorService`
3. Tools auto-discovered on startup

## Timer System

The timer system lets the LLM schedule future actions:
- `SetDelayedResponse` - "Give me a recipe in 5 minutes"
- `SetReminder` - Simple reminders
- `SetEntertainmentMode` - Periodic entertainment

Timer stores the original user request and replays it to the LLM when it expires.

## Running

```bash
# Terminal 1 - Start your MCP server
cd McpServerRefrigerator
dotnet run

# Terminal 2 - Start agent
cd MewAgent
dotnet run
```

The agent uses official MCP SDK with HTTP transport to connect to your MCP server at `http://localhost:5100`.

## Design Decisions

- **No Shared Project**: Each project owns its models - simpler for juniors
- **Mock Server**: Testing MCP integration, not real refrigerator control  
- **Simple Timer**: Background thread, not a full scheduler
- **Simple HTTP MCP**: Direct HTTP calls for reliable tool integration with Semantic Kernel

That's it. Simple agent for testing MCP with Semantic Kernel.