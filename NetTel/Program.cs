using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;

var builder = WebApplication.CreateBuilder(args);

Action<ResourceBuilder> configureResource = r => r.AddService(
    serviceName: "copLog",//builder.Configuration.GetValue("ServiceName", defaultValue: "otel-test")!,
    serviceVersion: typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
    serviceInstanceId: Environment.MachineName);

builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;

    var resourceBuilder = ResourceBuilder.CreateDefault();
    configureResource(resourceBuilder);
    options.SetResourceBuilder(resourceBuilder);

    options.AddConsoleExporter();
    options.AddOtlpExporter(otlpOptions =>
        {
            otlpOptions.Endpoint = new Uri(builder.Configuration["Otpl:LogEndpoint"] ?? throw new Exception());
        });
});

builder.Services.AddLogging(options =>
{
    options.SetMinimumLevel(LogLevel.Debug);
    options.ClearProviders();
    options.AddConsole();
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
            opt.Endpoint = new Uri(builder.Configuration["Otpl:MetricEndpoint"] ?? throw new Exception());
        })
    )
    .WithTracing(opt =>
        opt
        .ConfigureResource(resource => resource.AddService("NetTelTrace"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddJaegerExporter(opt =>
        {
            opt.Endpoint = new Uri(builder.Configuration["Otpl:TracingEndpoint"] ?? throw new Exception());
        })

    );


var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", (ILogger<Program> log) =>
{
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
