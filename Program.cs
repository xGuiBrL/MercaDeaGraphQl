using MercaDeaGraphQl.GraphQL;
using MercaDeaGraphQl.Models.Data;
using MercaDeaGraphQl.Models.Security;
using MercaDeaGraphQl.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

using HotChocolate;
using HotChocolate.AspNetCore;
using HotChocolate.Data;
using HotChocolate.Types;

using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ⭐ Render usa un puerto dinámico – configurar el puerto aquí
var port = Environment.GetEnvironmentVariable("PORT") ?? "10000";
builder.WebHost.UseUrls($"http://*:{port}");

// MongoDB
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("DatabaseSettings"));
builder.Services.AddSingleton<MongoDbContext>();

// JWT
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("JwtSettings"));
var jwtSettings = builder.Configuration
    .GetSection("JwtSettings")
    .Get<JwtSettings>();

var key = Encoding.UTF8.GetBytes(jwtSettings.SecretKey);

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.SaveToken = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
    });

builder.Services.AddSingleton<JwtService>();
builder.Services.AddAuthorization();

// ⭐ CORS – PERMITIR TODO
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy => policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// GraphQL
builder.Services
    .AddGraphQLServer()
    .AddQueryType<Query>()
    .AddMutationType<Mutation>()
    .AddUploadType()
    .AddAuthorization()
    .AddFiltering()
    .AddSorting()
    .ModifyRequestOptions(o => o.IncludeExceptionDetails = true);

builder.Services.AddControllers();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();

// ⭐ APLICAR CORS AQUÍ — ANTES DE AUTH Y GRAPHQL
app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

// ⭐ SOLO UNA VEZ
app.MapGraphQL("/graphql");
app.MapControllers();

app.Run();