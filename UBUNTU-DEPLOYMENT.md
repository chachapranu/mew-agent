# Ubuntu Deployment Guide for Mew Agent

## Overview
MewAgent is a **console/desktop application** that runs perfectly on Ubuntu. It consists of:
- **MewAgent**: Console AI assistant (desktop app)
- **McpServerRefrigerator**: Backend MCP server (runs locally)

## Prerequisites
- Ubuntu 20.04 or later
- .NET 8 SDK installed

## Quick Start

### Option 1: Run with Scripts
```bash
# make scripts executable
chmod +x run-local.sh deploy-ubuntu.sh

# run directly with dotnet
./run-local.sh
```

### Option 2: Build Self-Contained Executables
```bash
# build standalone executables (no .NET required on target machine)
./deploy-ubuntu.sh

# run the executables
./publish/mcpserver/McpServerRefrigerator --urls=http://localhost:5100 &
./publish/mewagent/MewAgent
```

### Option 3: Run as System Services
```bash
# copy service files
sudo cp mewagent.service /etc/systemd/system/
sudo cp mcpserver.service /etc/systemd/system/

# edit service files to set your username
sudo nano /etc/systemd/system/mewagent.service
sudo nano /etc/systemd/system/mcpserver.service

# reload and start services
sudo systemctl daemon-reload
sudo systemctl enable mcpserver
sudo systemctl enable mewagent
sudo systemctl start mcpserver
sudo systemctl start mewagent

# check status
sudo systemctl status mcpserver
sudo systemctl status mewagent

# view logs
journalctl -u mewagent -f
journalctl -u mcpserver -f
```

## Configuration

Edit `MewAgent/appsettings.json` to configure:
- OpenAI/OpenRouter API keys
- Model selection
- MCP server URL (default: http://localhost:5100)

## Running on Ubuntu Desktop

For desktop use with GUI terminal:
```bash
# option 1: run in gnome-terminal
gnome-terminal -- bash -c "./run-local.sh; exec bash"

# option 2: run in separate terminals
# terminal 1
cd McpServerRefrigerator
dotnet run --urls="http://localhost:5100"

# terminal 2  
cd MewAgent
dotnet run
```

## Directory Structure
```
mew-agent/
├── MewAgent/           # Console application (desktop app)
├── McpServerRefrigerator/  # Local MCP server
├── run-local.sh        # Quick start script
├── deploy-ubuntu.sh    # Build standalone executables
├── mewagent.service    # Systemd service for agent
└── mcpserver.service   # Systemd service for server
```

## Architecture on Ubuntu

```
[Ubuntu Desktop/Terminal]
         │
    [MewAgent Console App]
         │
    [Semantic Kernel]
         │
    [MCP Client] ──HTTP──> [MCP Server:5100]
         │                        │
    [OpenRouter LLM]     [Refrigerator Service]
```

## Troubleshooting

### Port Already in Use
```bash
# find process using port 5100
sudo lsof -i :5100
# kill the process
kill -9 <PID>
```

### Permission Issues
```bash
# make scripts executable
chmod +x *.sh
```

### .NET Not Found
```bash
# install .NET 8 SDK
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
export PATH="$HOME/.dotnet:$PATH"
```

## Notes
- This is NOT a web application - it's a console/desktop app
- MCP Server runs locally, not exposed to network
- All processing happens on your Ubuntu machine
- No browser needed - runs in terminal