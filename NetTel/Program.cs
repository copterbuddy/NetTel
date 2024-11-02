using DTO.User.Register;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Security.Claims;

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

        builder.Services.AddOpenTelemetry()
            .WithMetrics(opt =>
            opt
                .ConfigureResource(resource => resource.AddService("NetTelMertric"))
                .AddMeter("NetTel")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
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

        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        builder.Services.AddAuthentication(o =>
        {
            o.DefaultScheme = "Application";
            o.DefaultSignInScheme = "External";
        })
        .AddCookie("Application", options =>
        {
            options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
            options.Cookie.HttpOnly = true;
            options.SlidingExpiration = true;
            if (builder.Environment.IsProduction())
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            }
        })
        .AddCookie("External", options =>
        {
            options.Cookie.HttpOnly = true;
            if (builder.Environment.IsProduction())
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromMinutes(15);
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = 401;
                    return Task.CompletedTask;
                };
            }
        })
        .AddGoogle(o =>
        {
            o.ClientId = builder.Configuration.GetValue<string>("ClientId");
            o.ClientSecret = builder.Configuration.GetValue<string>("ClientSecret");
        });
        builder.Services.AddAuthorization();

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

        app.MapGet("/GoogleLogin/Index", (HttpContext httpContext) =>
        {
            var result = Results.Challenge(
                new AuthenticationProperties
                {
                    RedirectUri = "/GoogleLogin/GoogleResponse"
                },
                [GoogleDefaults.AuthenticationScheme]
            );
            return result;
        })
        .RequireCors(AllowAll);

        app.MapGet("/GoogleLogin/GoogleResponse", async (HttpContext httpContext) =>
        {
            var authenticateResult = await httpContext.AuthenticateAsync("External");
            if (!authenticateResult.Succeeded)
                return Results.BadRequest("Error Authen"); // TODO: Handle this better.
                                                           //Check if the redirection has been done via google or any other links
            if (authenticateResult.Principal.Identities.ToList()[0].AuthenticationType!.ToLower() == "google")
            {
                if (authenticateResult.Principal != null)
                {
                    //get google account id for any operation to be carried out on the basis of the id
                    var googleAccountId = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                    //claim value initialization as mentioned on the startup file with o.DefaultScheme = "Application"
                    var claimsIdentity = new ClaimsIdentity("Application");

                    //check if principal value exists or not 
                    if (authenticateResult.Principal != null)
                    {
                        //Now add the values on claim and redirect to the page to be accessed after successful login
                        var details = authenticateResult.Principal.Claims.ToList();
                        claimsIdentity.AddClaim(authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)!);// Full Name Of The User
                        claimsIdentity.AddClaim(authenticateResult.Principal.FindFirst(ClaimTypes.Email)!); // Email Address of The User
                        await httpContext.SignInAsync
                        (
                            scheme: "Application",
                            principal: new ClaimsPrincipal(claimsIdentity),
                            properties: new AuthenticationProperties
                            {
                                IsPersistent = true, // หากต้องการให้คุกกี้อยู่ต่อหลังจากปิดเบราว์เซอร์
                                ExpiresUtc = DateTime.UtcNow.AddMinutes(30) // ตั้งเวลาหมดอายุใหม่
                            }
                        );

                        return Results.Redirect("http://localhost:4200/");
                    }
                }
            }

            return Results.Ok("Home");
        })
        .RequireCors(AllowAll);

        app.MapGet("/GoogleLogin/SignOutFromGoogleLogin", async (HttpContext httpContext) =>
        {
            if (httpContext.Request.Cookies.Count > 0)
            {
                //Check for the cookie value with the name mentioned for authentication and delete each cookie
                var siteCookies = httpContext.Request.Cookies.Where(c => c.Key.Contains(".AspNetCore.") || c.Key.Contains("Microsoft.Authentication"));
                foreach (var cookie in siteCookies)
                {
                    httpContext.Response.Cookies.Delete(cookie.Key);
                }
            }
            //signout with any cookie present 
            await httpContext.SignOutAsync("External");
            return Results.Ok("Home");
        })
        .RequireCors(AllowAll);

        app.MapGet("/GoogleLogin/GetInfo", (HttpContext httpContext) =>
        {
            if (httpContext.User.Identity?.IsAuthenticated == true)
            {
                // ส่งข้อมูล user กลับถ้าผู้ใช้ login แล้ว
                var userInfo = new
                {
                    Email = httpContext.User.FindFirst(ClaimTypes.Email)?.Value
                };

                return Results.Ok(userInfo); // ส่งข้อมูลผู้ใช้ไปให้ frontend
            }
            else
            {
                // ถ้าไม่ login ส่ง StatusCode 401
                return Results.Unauthorized();
            }
        })
        .RequireCors(AllowAll);

        app.MapHealthChecks("/healthz");

        app.MapPrometheusScrapingEndpoint();

        app.Run();
    }
}

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
