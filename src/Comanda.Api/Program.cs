using System.Text;
using System.Text.Json.Serialization;
using Comanda.Api.Common;
using Comanda.Domain.Abstractions;
using Comanda.Infrastructure;
using Comanda.Infrastructure.Persistence;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ---- Infraestructura (EF Core + Postgres, repos, seguridad) ----
builder.Services.AddInfrastructure(builder.Configuration);

// Cifrado de secretos por tenant (llaves Wompi)
builder.Services.AddDataProtection();
builder.Services.AddSingleton<Comanda.Domain.Abstractions.ISecretProtector, Comanda.Api.Common.DataProtectionSecretProtector>();

// ---- Autenticación JWT ----
var jwt = builder.Configuration.GetSection("Jwt");
var signingKey = jwt["SigningKey"]
    ?? throw new InvalidOperationException("Falta 'Jwt:SigningKey' (configúralo por variable de entorno Jwt__SigningKey).");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt["Issuer"] ?? "Comanda",
            ValidateAudience = true,
            ValidAudience = jwt["Audience"] ?? "ComandaClients",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ---- FastEndpoints + RMapper + Swagger ----
builder.Services.AddFastEndpoints();
builder.Services.AddRMapper(typeof(Program).Assembly);
builder.Services.SwaggerDocument(o =>
{
    o.EnableJWTBearerAuth = true;
    o.DocumentSettings = s =>
    {
        s.Title = "Comanda API";
        s.Version = "v1";
        s.Description = "Backend de la plataforma de gestión de restaurantes Comanda (El Salvador).";
    };
});

// ---- CORS para los clientes Angular ----
// En producción los orígenes vienen por config (env var Cors__Origins, separados por ';'),
// p.ej. "https://app.comanda.sv;https://admin.comanda.sv". Sin config → defaults de desarrollo.
const string clientCors = "comanda-client";
var corsOrigins = (builder.Configuration["Cors:Origins"] ?? "http://localhost:4200;http://localhost:4789;http://localhost:4300")
    .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy(clientCors, p => p
    .WithOrigins(corsOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// ---- Migración + seed automáticos (datos salvadoreños) ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ComandaDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
    var tenant = scope.ServiceProvider.GetRequiredService<ICurrentTenant>();
    await DbSeeder.SeedAsync(db, hasher, tenant);
    await DbSeeder.EnsureHistoricalOrdersAsync(db);
}

app.UseCors(clientCors);
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.UseFastEndpoints(c =>
{
    c.Endpoints.RoutePrefix = "api";
    c.Serializer.Options.Converters.Add(new JsonStringEnumConverter());
});

app.UseSwaggerGen();

app.Run();

// Necesario para AddRMapper(typeof(Program).Assembly) y para pruebas de integración.
public partial class Program;
