using AspNetCoreRateLimit;
using MessengerServer.Data;
using MessengerServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve);
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddSingleton<ConnectionManager>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<EncryptionService>();
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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddInMemoryRateLimiting();

var app = builder.Build();

app.UseIpRateLimiting();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseWebSockets();

app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var socket = await context.WebSockets.AcceptWebSocketAsync();
        var connectionId = Guid.NewGuid().ToString();

        var connectionManager = context.RequestServices.GetRequiredService<ConnectionManager>();
        connectionManager.AddConnection(connectionId, socket);

        await HandleWebSocketConnectionAsync(socket, connectionManager, connectionId);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
builder.Logging.AddConsole();

app.Run();


static async Task HandleWebSocketConnectionAsync(WebSocket socket, ConnectionManager connectionManager, string connectionId)
{
    var buffer = new byte[1024 * 4];

    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

        if (result.MessageType == WebSocketMessageType.Text)
        {
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
            Console.WriteLine($"Received message: {message}");
        }
        else if (result.MessageType == WebSocketMessageType.Close)
        {
            connectionManager.RemoveConnection(connectionId);
            await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closed by client", CancellationToken.None);
        }
    }

    connectionManager.RemoveConnection(connectionId);
}