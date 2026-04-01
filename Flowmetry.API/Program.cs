using Flowmetry.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
var corsOrigins = new List<string> { "http://localhost:5173" };
var vercelOrigin = Environment.GetEnvironmentVariable("ALLOWED_ORIGIN_VERCEL");
if (!string.IsNullOrWhiteSpace(vercelOrigin))
{
    corsOrigins.Add(vercelOrigin);
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowUI", policy =>
    {
        policy.WithOrigins(corsOrigins.ToArray())
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flowmetry API v1");
    c.EnableTryItOutByDefault();
});

app.UseCors("AllowUI");

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.Run();

public partial class Program { }
