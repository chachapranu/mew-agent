using McpServerRefrigerator.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "MCP Server - Smart Refrigerator", Version = "v1" });
});

// Register our services
builder.Services.AddSingleton<RefrigeratorService>();
builder.Services.AddSingleton<ToolExecutionService>();

// Configure CORS for cross-origin requests (for when agent is on different machine)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowHomeFlowAgent",
        policy =>
        {
            policy.AllowAnyOrigin()
                  .AllowAnyMethod()
                  .AllowAnyHeader();
        });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "MCP Server v1");
    });
}

app.UseCors("AllowHomeFlowAgent");

app.UseRouting();

app.MapControllers();

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("MCP Server - Smart Refrigerator started");
logger.LogInformation($"Server running on: {builder.Configuration["Urls"] ?? "http://localhost:5000"}");
logger.LogInformation("Available endpoints:");
logger.LogInformation("  GET  /api/mcp/tools   - Discover available tools (legacy)");
logger.LogInformation("  POST /api/mcp/execute - Execute a tool (legacy)");
logger.LogInformation("  POST /sse            - MCP Server-Sent Events endpoint");
logger.LogInformation("  GET  /api/mcp/health - Health check");

app.Run();