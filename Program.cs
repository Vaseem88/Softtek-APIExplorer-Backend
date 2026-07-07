using Softtek_APIExplorer_Backend.Middleware;
using Softtek_APIExplorer_Backend.Services;

var builder = WebApplication.CreateBuilder(args);


const string APIFrontEnd = "APIFrontEnd";
var frontendUrl = builder.Configuration["Cors:FrontendUrl"]
    ?? throw new InvalidOperationException("Missing configuration: Cors:FrontendUrl");

builder.Services.AddCors(options =>
{
    options.AddPolicy(APIFrontEnd, policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

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
builder.Services.AddScoped<AIService>();
builder.Services.AddScoped<TextEmbeddingAIService>();
builder.Services.AddSingleton<IEnterpriseLlmClient, MockEnterpriseLlmClient>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors(APIFrontEnd);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
