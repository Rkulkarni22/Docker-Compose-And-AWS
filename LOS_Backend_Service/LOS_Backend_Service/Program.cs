using LOS_Backend_Service.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LosDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();   // ADD THIS
builder.Services.AddSwaggerGen();             // ADD THIS

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "LOS Backend Service API v1");
        //c.RoutePrefix = string.Empty;   // <-- this makes Swagger the root page
    });
}

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseAuthorization();

app.MapControllers();

app.Run();
