using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders().AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;

    var resourceBuilder = ResourceBuilder.CreateDefault().AddService("NetTelLog");
    // configureResource(resourceBuilder);
    options.SetResourceBuilder(resourceBuilder);

    options.AddConsoleExporter();
    options.AddOtlpExporter(otlpOptions =>
        {
            string headerKey = "signoz-access-token";
            string headerValue = "<SIGNOZ_INGESTION_KEY>";

            otlpOptions.Headers = $"{headerKey}={headerValue}";

            otlpOptions.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"] ?? throw new Exception("Otpl:Endpoint cannot be null"));
        });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddOpenTelemetry()
    .WithMetrics(opt =>
    opt
        .ConfigureResource(resource => resource.AddService("NetTelMertric"))
        .AddMeter("NetTel")
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"] ?? throw new Exception("Otpl:Endpoint cannot be null"));
        })
    )
    .WithTracing(opt =>
        opt
        .ConfigureResource(resource => resource.AddService("NetTelTrace"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"] ?? throw new Exception("Otpl:Endpoint cannot be null"));
        })

    );


var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPrometheusScrapingEndpoint();

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> log) =>
{
    var status = "Running";
    var currentTime = DateTime.UtcNow.ToString();

    log.LogInformation($"Application Status changed to '{status}' at '{currentTime}'");

    log.LogWarning("this is original log");
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
