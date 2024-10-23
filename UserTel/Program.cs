using DTO.User.Register;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

string AllowAll = "_Allow_All";

builder.Services.AddCors(option =>
{
    option.AddPolicy(AllowAll,
        policy =>
        {
            policy.AllowAnyOrigin();
            policy.AllowAnyHeader();
            policy.AllowAnyMethod();
        });
});

builder.Services.AddHealthChecks();

builder.Logging.ClearProviders().AddOpenTelemetry(options =>
{
    options.IncludeScopes = true;
    options.IncludeFormattedMessage = true;

    var resourceBuilder = ResourceBuilder.CreateDefault().AddService("UserTelLog");
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

builder.Services.AddOpenTelemetry()
    .WithMetrics(opt =>
    opt
        .ConfigureResource(resource => resource.AddService("UserTelMertric"))
        .AddMeter("UserTel")
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddHttpClientInstrumentation()
        .AddProcessInstrumentation()
        .AddPrometheusExporter()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"] ?? throw new Exception("Otpl:Endpoint cannot be null"));
        })
    )
    .WithTracing(opt =>
        opt
        .ConfigureResource(resource => resource.AddService("UserTelTrace"))
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(opt =>
        {
            opt.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"] ?? throw new Exception("Otpl:Endpoint cannot be null"));
        })

    );

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseCors(AllowAll);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapPost("/users/register", ([FromBody] User user) =>
{
    return $"Register {user.UserName} {user.Password}";
})
.WithName("UserRegister")
.WithOpenApi()
.RequireCors(AllowAll);

app.MapHealthChecks("/healthz");

app.MapPrometheusScrapingEndpoint();

app.Run();
