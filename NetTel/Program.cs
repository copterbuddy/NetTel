using DTO.User.Register;
using Newtonsoft.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

internal class Program
{
    private static void Main(string[] args)
    {
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

        app.UseCors(AllowAll);

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
        }).WithName("GetWeatherForecast")
        .WithOpenApi();

        app.MapPost("/user/register", async () =>
        {
            string? result;
            try
            {
                User reqBody = new()
                {
                    UserName = "user",
                    Password = "password",
                };

                string requestBody = "";
                using var client = new HttpClient();
                var userTelEndpoint = builder.Configuration["UserTel:Endpoint"] ?? throw new Exception("UserTel:Endpoint cannot be null");
                client.BaseAddress = new Uri(userTelEndpoint);
                var response = await client.PostAsJsonAsync("users/register", reqBody);
                if (response.IsSuccessStatusCode == false)
                {
                    //log
                }

                result = await response.Content.ReadAsStringAsync();

            }
            catch (Exception)
            {

                throw;
            }

            return result;
        }).WithName("MainUserRegister")
        .WithOpenApi()
        .RequireCors(AllowAll);

        app.MapHealthChecks("/healthz");

        app.Run();
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
