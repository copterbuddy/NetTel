using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using telMain.Components;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

        builder.Services.AddOpenTelemetry()
            .WithMetrics(opt =>
            opt
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("OpenRemoteManage.GatewayAPI"))
                .AddMeter("OpenRemoteManagerMeter")
                .AddAspNetCoreInstrumentation()
                .AddRuntimeInstrumentation()
                .AddProcessInstrumentation()
                .AddOtlpExporter(opt =>
                {
                    opt.Endpoint = new Uri(builder.Configuration["Otpl:Endpoint"]);
                })
            );

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error", createScopeForErrors: true);
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        app.UseStaticFiles();
        app.UseAntiforgery();

        app.MapRazorComponents<App>()
            .AddInteractiveServerRenderMode();

        app.Run();
    }
}