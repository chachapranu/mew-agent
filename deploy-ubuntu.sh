#!/bin/bash

# Mew Agent Ubuntu Deployment Script
# Builds self-contained executables for Ubuntu

echo "Building Mew Agent for Ubuntu..."

# clean previous builds
rm -rf ./publish

# build mewagent as self-contained executable
echo "Building MewAgent console application..."
cd MewAgent
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ../publish/mewagent
cd ..

# build mcp server as self-contained executable  
echo "Building MCP Server..."
cd McpServerRefrigerator
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o ../publish/mcpserver
cd ..

echo "Build complete!"
echo ""
echo "Deployment files created in ./publish/"
echo "  - mewagent/MewAgent    (Console AI Agent)"
echo "  - mcpserver/McpServerRefrigerator    (MCP Server)"
echo ""
echo "To run on Ubuntu:"
echo "  1. Start MCP Server: ./publish/mcpserver/McpServerRefrigerator --urls=http://localhost:5100"
echo "  2. Start Agent: ./publish/mewagent/MewAgent"