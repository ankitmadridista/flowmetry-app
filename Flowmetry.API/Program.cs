using Flowmetry.API.Endpoints;
using Flowmetry.Application;
using Flowmetry.Infrastructure;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddProblemDetails();

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

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// Global exception handler — returns RFC 7807 ProblemDetails
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;

        var (statusCode, title) = exception switch
        {
            InvalidOperationException => (StatusCodes.Status422UnprocessableEntity, "Unprocessable Entity"),
            ArgumentException => (StatusCodes.Status400BadRequest, "Bad Request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = exception?.Message,
            Instance = context.Request.Path
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    });
});

// Configure the HTTP request pipeline.
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Flowmetry API v1");
    c.EnableTryItOutByDefault();
});

app.UseCors("AllowUI");

// Auto-apply pending migrations on startup (safe for Render / single-instance deploys)
// Skip for InMemory provider used in tests
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<Flowmetry.Infrastructure.FlowmetryDbContext>();
    if (db.Database.IsRelational())
        db.Database.Migrate();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapInvoiceEndpoints();
app.MapDashboardEndpoints();

app.Run();

public partial class Program { }
