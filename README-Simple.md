# 🐱 Mew Agent - Smart Home AI Assistant

A modern AI agent built with Microsoft Semantic Kernel that demonstrates clean architecture patterns for LLM orchestration and tool integration.

## 🚀 Quick Start

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

## 🏗️ Architecture Overview

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
                       ┌────────────────┐
                       │ SimpleMcpService│
                       │   (Mock Tools)  │
                       └────────────────┘
```

### Key Files
| File | Purpose |
|------|---------|
| `Program.cs` | Entry point, dependency injection setup |
| `Services/MewAgentService.cs` | Main orchestration, SK management |
| `Services/SimpleMcpService.cs` | Tool definitions and mock implementations |
| `appsettings.json` | Configuration for LLM and agent behavior |

## 🎯 Design Principles

### 1. **Semantic Kernel Integration**
- Uses SK's native plugin system with `KernelFunctionFactory`
- Automatic tool calling via `ToolCallBehavior.AutoInvokeKernelFunctions`
- Conversation memory with `ChatHistory`

### 2. **Custom LLM Support**
- Works with any OpenAI-compatible API
- Configurable endpoints for local/cloud LLMs
- Easy switching between models

### 3. **Clean Architecture**
- Dependency injection throughout
- Separation of concerns (UI, orchestration, tools)
- Minimal, focused services

### 4. **Tool System**
The agent has access to these refrigerator tools:
- **GetTemperature** - Check current fridge/freezer temps
- **SetTemperature** - Adjust temperature settings
- **GetDiagnostics** - System health and maintenance info
- **GetInventory** - Food inventory tracking
- **GetRecipeSuggestions** - AI-powered recipe ideas

## 💻 Usage Examples

### Basic Interactions
```
You: What's the current temperature?
Mew: Let me check the refrigerator temperature for you...

You: What food do I have?
Mew: I'll check your current inventory...

You: Suggest a recipe for dinner
Mew: Based on your available ingredients, here are some ideas...
```

### Debug Commands
- `/help` - Show available commands
- `/tools` - List all available tools
- `/memory` - Show conversation history count
- `/clear` - Reset conversation context
- `/quit` - Exit the application

## 🔧 Configuration

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

## 🧪 Extending the Agent

### Adding New Tools
1. Add function to `SimpleMcpService.CreatePluginAsync()`:
```csharp
functions.Add(CreateMockFunction(
    "NewTool",
    "Description of what this tool does",
    async (args) => {
        // Tool implementation
        return "Tool result";
    }));
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

## 📊 Performance & Memory

- **Conversation History**: Kept in memory, grows with usage
- **Token Management**: Configurable max tokens per response
- **HTTP Client**: Reused via dependency injection
- **Tool Execution**: Mock delays for realistic simulation

## 🔐 Security Notes

- Store API keys in environment variables, not source code
- Semantic Kernel provides built-in prompt injection protection
- Use HTTPS endpoints for production deployments

## 📚 Learning Resources

- [Semantic Kernel Documentation](https://learn.microsoft.com/semantic-kernel/)
- [OpenAI API Reference](https://platform.openai.com/docs/)
- [Dependency Injection in .NET](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)

## 🎓 What This Demonstrates

This project showcases:
- **Modern AI Orchestration** with Semantic Kernel
- **Custom LLM Integration** beyond just OpenAI
- **Plugin Architecture** for extensible tool systems
- **Clean Code Practices** with minimal, focused classes
- **Async/Await Patterns** throughout the application
- **Dependency Injection** for testable, modular design

Perfect for learning AI agent development with .NET!