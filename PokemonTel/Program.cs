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

    var resourceBuilder = ResourceBuilder.CreateDefault().AddService("PokemonTelLog");
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
        .ConfigureResource(resource => resource.AddService("PokemonTelMertric"))
        .AddMeter("PokemonTel")
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
        .ConfigureResource(resource => resource.AddService("PokemonTelTrace"))
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

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(AllowAll);

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/pokemons",async () =>
{
    string? result;
    try
    {
        using var client = new HttpClient();
        var pokemonApiEndpoint = builder.Configuration["PokemonApi:Endpoint"] ?? throw new Exception("PokemonApi:Endpoint cannot be null");
        client.BaseAddress = new Uri(pokemonApiEndpoint);
        var response = await client.GetAsync("api/v2/pokemon?limit=10&offset=0");
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
})
.WithName("GetPokemons")
.WithOpenApi()
.RequireCors(AllowAll);

app.MapGet("/pokemon/{name}/detail", async (string name) =>
{
    string? result;
    try
    {
        using var client = new HttpClient();
        var pokemonApiEndpoint = builder.Configuration["PokemonApi:Endpoint"] ?? throw new Exception("PokemonApi:Endpoint cannot be null");
        client.BaseAddress = new Uri(pokemonApiEndpoint);
        var response = await client.GetAsync($"api/v2/pokemon/{name}");
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
})
.WithName("GetPokemonDetail")
.WithOpenApi()
.RequireCors(AllowAll);

app.MapHealthChecks("/healthz");

app.MapPrometheusScrapingEndpoint();

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
