# Mew Agent - Smart Home AI Agent System

A learning project demonstrating agent orchestration with Microsoft Semantic Kernel and MCP (Model Context Protocol) for smart home device interaction.

## 🎯 Project Overview

Mew Agent is a console-based AI agent that communicates with a smart refrigerator via MCP/HTTP protocols. It demonstrates:
- **Semantic Kernel**: Native MCP client support with automatic fallback to HTTP
- **MCP Protocol**: Built-in ModelContextProtocol package integration
- **Plugin Architecture**: Hybrid approach supporting both MCP and HTTP endpoints
- **AI Integration**: Natural language processing with OpenAI GPT models
- **Async Patterns**: C# async/await for network communication

## 📁 Project Structure

```
/MewAgentSolution/
├── MewAgent/                # Console app with Semantic Kernel
├── McpServerRefrigerator/   # Web API with MCP tools
└── Shared/                  # Common models and contracts
```

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- OpenAI API key (for the agent)
- Two terminal windows

### Configuration

1. **Update OpenAI Settings** in `MewAgent/appsettings.json`:
```json
"OpenAI": {
    "ApiKey": "YOUR_ACTUAL_OPENAI_API_KEY",
    "ModelId": "gpt-4",  // or "gpt-3.5-turbo"
    "Endpoint": "https://api.openai.com/v1"
}
```

### Running the System

1. **Start the MCP Server** (Terminal 1):
```bash
cd McpServerRefrigerator
dotnet run
```
The server will start on http://localhost:5100 with Swagger UI available at http://localhost:5100/swagger

2. **Start the Mew Agent** (Terminal 2):
```bash
cd MewAgent
dotnet run
```

## 💬 Using the Agent

### Example Conversations
- "What's the current temperature?"
- "Show me what food I have"
- "Suggest a recipe for dinner"
- "Check the system diagnostics"
- "Set the fridge temperature to 38 degrees"
- "I want to cook for 2 hours" (triggers extended engagement)

### Debug Commands
- `/help` - Show available commands
- `/tools` - List discovered MCP tools
- `/status` - Check system connection status
- `/memory` - Show conversation memory usage
- `/clear` - Clear conversation history
- `/quit` - Exit the application

## 🔧 Available Tools

The MCP Server provides these tools:
1. **GetTemperature** - Check current fridge/freezer temperatures
2. **SetTemperature** - Adjust temperature settings
3. **GetDiagnostics** - System health and maintenance info
4. **GetInventory** - View food inventory
5. **GetRecipeSuggestions** - Get recipes based on available ingredients

## 🏗️ Architecture

### Communication Flow
1. User inputs natural language to the console agent
2. Semantic Kernel processes the input with the AI model
3. SK automatically invokes MCP tools via HTTP when needed
4. MCP server executes tools and returns JSON responses
5. Agent synthesizes the response for the user

### Key Components
- **Semantic Kernel**: Orchestrates AI and tool calling with native MCP support
- **HybridMcpService**: Uses SK's MCP client with HTTP fallback
- **ModelContextProtocol Package**: Official .NET MCP client library
- **Refrigerator Plugin**: SK plugin exposing tools as kernel functions
- **Tool Execution Service**: Handles tool routing and mock data

## 📝 Development Notes

### Adding New Tools
1. Add tool definition in `ToolExecutionService.GetAvailableTools()`
2. Implement tool logic in `RefrigeratorService`
3. Add execution case in `ToolExecutionService.ExecuteToolAsync()`
4. Create corresponding KernelFunction in `RefrigeratorPlugin`

### MCP vs HTTP Operation

The system first attempts to connect using Semantic Kernel's native MCP client:
1. **MCP Protocol**: Tries SSE connection to `/sse` endpoint
2. **HTTP Fallback**: Falls back to REST API if MCP fails
3. **Transparent Operation**: Same tools, different transport layers

### Testing the Server
Use the Swagger UI at http://localhost:5100/swagger or test with curl:
```bash
# Get available tools (HTTP fallback)
curl http://localhost:5100/api/mcp/tools

# Execute a tool (HTTP fallback)
curl -X POST http://localhost:5100/api/mcp/execute \
  -H "Content-Type: application/json" \
  -d '{"toolName":"GetTemperature","parameters":{}}'
```

## 🎓 Learning Outcomes

This project demonstrates:
- **Semantic Kernel MCP Integration**: Using SK's native MCP client support
- **ModelContextProtocol .NET**: Official MCP client library usage
- **Hybrid Architecture**: MCP-first with HTTP fallback approach
- **Plugin Architecture**: Dynamic tool loading via `KernelFunctionFactory`
- **Automatic Function Calling**: `ToolCallBehavior.AutoInvokeKernelFunctions`
- **Conversation Memory**: `ChatHistory` management
- **Dependency Injection**: Console application service registration
- **Mock Data Services**: Testing AI interactions with realistic delays

## 🔮 Future Enhancements

- [ ] Server-Sent Events (SSE) for real-time updates
- [ ] Additional tool categories (entertainment, calendar)
- [ ] Persistent conversation memory
- [ ] Multi-device coordination
- [ ] Voice integration
- [ ] Real appliance API integration

## 📄 License

This is a learning project for educational purposes.