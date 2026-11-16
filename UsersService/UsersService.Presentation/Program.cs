using Microsoft.EntityFrameworkCore;
using UsersService.Infrastructure.DataAccess;
using UsersService.Application;
using UsersService.Application.Abstractions;
using UsersService.Infrastructure;
using UsersService.Infrastructure.Security;
using UsersService.Presentation.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection"));
builder.Services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();

builder.Services
    .AddOptions<JwtOptions>()
    .BindConfiguration(JwtOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddApplication();

var app = builder.Build();

app.Services.ApplyMigrations();

app.UseGlobalExceptionHandling();

app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<UsersDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

public partial class Program { }
