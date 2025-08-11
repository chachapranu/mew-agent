#!/bin/bash

# Simple script to run Mew Agent locally on Ubuntu
# Starts both MCP server and agent

echo "Starting Mew Agent System..."

# start mcp server in background
echo "Starting MCP Server on port 5100..."
cd McpServerRefrigerator
dotnet run --urls="http://localhost:5100" &
SERVER_PID=$!
cd ..

# wait for server to start
echo "Waiting for server to initialize..."
sleep 3

# start agent
echo "Starting Mew Agent..."
cd MewAgent
dotnet run

# cleanup - kill server when agent exits
echo "Shutting down MCP Server..."
kill $SERVER_PID 2>/dev/null

echo "Mew Agent stopped."