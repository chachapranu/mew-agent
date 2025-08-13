# Mew Agent - Console AI Assistant for Ubuntu

A pure console application built with Microsoft Semantic Kernel and Model Context Protocol (MCP). Designed for desktop/terminal use on Ubuntu and other Linux systems. No web hosting or browser required.

## Quick Start (Ubuntu/Linux)

### Prerequisites
- .NET 8 SDK
- OpenAI-compatible API key (OpenAI, OpenRouter, LocalAI, etc.)

### Setup
1. Clone and navigate to the project
2. Add your API configuration to `MewAgent/appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "your-api-key-here",
    "ModelId": "gpt-4",
    "Endpoint": "https://api.openai.com/v1"
  }
}
```

### Run
```bash
cd MewAgent
dotnet run
```

## Architecture Overview

### Core Components
```
┌─────────────────┐    ┌────────────────┐    ┌─────────────────┐
│   Console UI    │───▶│ MewAgentService│───▶│ Semantic Kernel │
└─────────────────┘    └────────────────┘    └─────────────────┘
                               │                       │
                               │                       ▼
                               │              ┌─────────────────┐
                               │              │   Custom LLM    │
                               │              │ (OpenRouter/etc)│
                               │              └─────────────────┘
                               ▼
                       ┌────────────────┐    ┌─────────────────┐
                       │ McpClientService│───▶│   MCP Server    │
                       │ (Real MCP Tools)│    │  (Port 5100)    │
                       └────────────────┘    └─────────────────┘
```

### Key Files
| File | Purpose |
|------|---------|
| `Program.cs` | Entry point, dependency injection setup |
| `Services/MewAgentService.cs` | Main orchestration, SK management |
| `Services/McpClientService.cs` | MCP client that calls real MCP server tools |
| `Services/TimerService.cs` | Internal timer system for proactive behavior |
| `Plugins/TimerPlugin.cs` | Semantic Kernel plugin for timer functionality |
| `Models/TimerModels.cs` | Timer data models and action types |
| `appsettings.json` | Configuration for LLM and agent behavior |

## Design Principles

### 1. **Proper MCP Integration**
- Real MCP client that discovers tools from MCP server
- HTTP transport communication with MCP server
- Dynamic tool registration via `KernelFunctionFactory`
- Semantic Kernel integration with `ToolCallBehavior.AutoInvokeKernelFunctions`

### 2. **Custom LLM Support**
- Works with any OpenAI-compatible API
- Configurable endpoints for local/cloud LLMs
- Easy switching between models

### 3. **Clean Architecture**
- Dependency injection throughout
- Separation of concerns (UI, orchestration, tools)
- Minimal, focused services

### 4. **MCP Tool System**
The agent discovers and calls these refrigerator tools from MCP server:
- **GetTemperature** - Check current fridge/freezer temps
- **SetTemperature** - Adjust temperature settings  
- **GetDiagnostics** - System health and maintenance info
- **GetInventory** - Food inventory tracking
- **GetRecipeSuggestions** - AI-powered recipe ideas

All tools are provided by the MCP server running on port 5100.

### 5. **Proactive Timer System**
The agent includes an intelligent timer system for proactive behavior:
- **Smart Task Execution** - Stores original user requests and replays them to LLM when timers expire
- **Delayed Responses** - "Give me a recipe in 2 minutes" automatically provides recipes later
- **Entertainment Mode** - "Entertain me for 2 hours" provides periodic jokes, facts, and games
- **Cooking Guidance** - Step-by-step cooking timers with automatic prompts
- **Flexible Reminders** - Any task can be scheduled with intelligent LLM execution

## Usage Examples

### Basic Interactions
```
You: What's the current temperature?
Mew: Let me check the refrigerator temperature for you...

You: What food do I have?
Mew: I'll check your current inventory...

You: Suggest a recipe for dinner
Mew: Based on your available ingredients, here are some ideas...
```

### Timer & Proactive Features
```
You: Give me a coffee recipe in 5 minutes
Mew: I'll provide a coffee recipe in 5 minutes at 14:25:30. Timer ID: a1b2c3d4

[5 minutes later]
Timer Alert: Here's a delicious coffee recipe for you: French Press Coffee...

You: Remind me to check the oven in 10 minutes
Mew: Reminder set for 10 minutes: 'check the oven' at 14:30:15. ID: e5f6g7h8

You: Entertain me for the next hour  
Mew: Entertainment mode activated for 1 hour! I've set 4 proactive interactions...
```

### Debug Commands
- `/help` - Show available commands
- `/tools` - List all available tools
- `/timers` - List active timers with details
- `/memory` - Show conversation history count
- `/clear` - Reset conversation context
- `/quit` - Exit the application

## Configuration

### LLM Settings
```json
{
  "OpenAI": {
    "ApiKey": "your-key",           // Required
    "ModelId": "gpt-4",             // Model to use
    "Endpoint": "https://..."       // Optional: custom endpoint
  },
  "Agent": {
    "MaxTokens": 4000,              // Response length limit
    "Temperature": 0.7,             // Creativity (0-1)
    "SessionTimeoutMinutes": 120    // Unused currently
  }
}
```

### Supported LLM Providers
- **OpenAI**: GPT-3.5, GPT-4, GPT-4 Turbo
- **OpenRouter**: Any model on their platform
- **LocalAI**: Self-hosted OpenAI-compatible server
- **Ollama**: Local LLM server
- **Any**: OpenAI-compatible API endpoint

## Extending the Agent

### Adding New Tools
1. Add tool definition to `McpServerRefrigerator/Services/ToolExecutionService.cs`
2. Implement tool logic in `RefrigeratorService.cs`
3. Tools are automatically discovered by MCP client on startup

```csharp
// In ToolExecutionService.GetAvailableTools()
new ToolDefinition {
    Name = "NewTool",
    Description = "Description of what this tool does",
    Category = "Tool Category"
}

// In ToolExecutionService.ExecuteToolAsync()
"NewTool" => await _refrigeratorService.NewToolAsync()
```

### Changing LLM Provider
Just update `appsettings.json`:
```json
{
  "OpenAI": {
    "ApiKey": "local-key-or-none",
    "ModelId": "llama-3.1-8b",
    "Endpoint": "http://localhost:11434/v1"  // Ollama
  }
}
```

## Performance & Memory

- **Conversation History**: Kept in memory, grows with usage
- **Token Management**: Configurable max tokens per response
- **HTTP Client**: Reused via dependency injection
- **Tool Execution**: Mock delays for realistic simulation

## Security Notes

- Store API keys in environment variables, not source code
- Semantic Kernel provides built-in prompt injection protection
- Use HTTPS endpoints for production deployments

## Learning Resources

- [Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel/)
- [OpenAI API Reference](https://platform.openai.com/docs/)
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

## What This Demonstrates

This project showcases:
- **Modern AI Orchestration** with Semantic Kernel
- **Custom LLM Integration** beyond just OpenAI
- **Plugin Architecture** for extensible tool systems
- **Proactive AI Behavior** with intelligent timer system
- **Model Context Protocol** for dynamic tool discovery
- **Clean Code Practices** with minimal, focused classes
- **Async/Await Patterns** throughout the application
- **Dependency Injection** for testable, modular design

Perfect for learning AI agent development with .NET on Linux desktop environments!