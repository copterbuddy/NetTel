using DTO.User.Register;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

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

app.Run();
