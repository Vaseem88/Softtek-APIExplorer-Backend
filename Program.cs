using Softtek_APIExplorer_Backend.Middleware;
using Softtek_APIExplorer_Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("PlaygroundProxyClient");
builder.Services.AddHttpClient("OpenApiSourceClient");

builder.Services.AddScoped<IPlaygroundOrchestratorService, PlaygroundOrchestratorService>();
builder.Services.AddScoped<IOpenApiSpecService, OpenApiSpecService>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IExecutionProxyService, ExecutionProxyService>();
builder.Services.AddScoped<ISemanticErrorService, SemanticErrorService>();
builder.Services.AddSingleton<IEnterpriseLlmClient, MockEnterpriseLlmClient>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
