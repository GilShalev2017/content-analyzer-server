using System.Net.WebSockets;
using ContentModerationAndAnalyticsPlatform.Services;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// App-specific services
builder.Services.AddSingleton<KafkaProducerService>();
builder.Services.AddSingleton<ElasticsearchService>();
builder.Services.AddSingleton<KafkaConsumerService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<KafkaConsumerService>());

builder.Services.AddSingleton<ContentAnalysisWorkerService>();
builder.Services.AddHostedService<ContentAnalysisWorkerService>();


// OPTIONAL: JWT Authentication - uncomment if needed
/*
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]))
        };
    });

builder.Services.AddAuthorization();
*/

// CORS - Allow everything (dev setup)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Capture the app-level service provider
var serviceProvider = app.Services;

app.UseWebSockets();

// Use Swagger in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/ws")
    {
        if (context.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

            // 💡 Pass the scoped service provider to the handler
            await HandleWebSocket(webSocket, serviceProvider);
        }
        else
        {
            context.Response.StatusCode = 400;
        }
    }
    else
    {
        await next();
    }
});

app.MapControllers();

app.Run();


// 🧠 Pass IServiceProvider into the handler
async Task HandleWebSocket(WebSocket webSocket, IServiceProvider serviceProvider)
{
    var buffer = new byte[1024 * 4];

    // 🔧 Use GetRequiredService to fail fast if service is missing
    var kafkaConsumer = serviceProvider.GetRequiredService<KafkaConsumerService>();

    kafkaConsumer.SetWebSocket(webSocket);

    while (webSocket.State == WebSocketState.Open)
    {
        try
        {
            var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

            if (receiveResult.CloseStatus.HasValue)
            {
                await webSocket.CloseAsync(receiveResult.CloseStatus.Value, receiveResult.CloseStatusDescription, CancellationToken.None);
                break;
            }

            var receivedMessage = System.Text.Encoding.UTF8.GetString(buffer, 0, receiveResult.Count);
            Console.WriteLine($"Received from client: {receivedMessage}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            break;
        }
    }
}
