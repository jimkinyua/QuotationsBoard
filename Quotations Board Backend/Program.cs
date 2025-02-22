using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables();
        // CORS

        var CORS_SPECIFICATIONS = "ALLOWED ROUTES";
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(name: CORS_SPECIFICATIONS,
                policy =>
                {
                    policy.AllowAnyOrigin()
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
        });


        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
            options.JsonSerializerOptions.DictionaryKeyPolicy = null;
        });

        var securityScheme = new OpenApiSecurityScheme()
        {
            Description = "Enter token here as: Bearer[space] your-token",
            Name = "Authorization",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
        };

        var securityRequirement = new OpenApiSecurityRequirement
        {
            {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] {}
            }
        };

        builder.Services.AddSwaggerGen(c =>
        {

            c.SwaggerDoc("v1", new() { Title = "Quatations Board", Version = "v1" });
            c.AddSecurityDefinition("Bearer", securityScheme);
            c.AddSecurityRequirement(securityRequirement);
            // Enable Annotations
            c.EnableAnnotations();
        });

        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        // Configure DB Context for Entity Framework
        builder.Services.AddDbContext<QuotationsBoardContext>(options =>
        {
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"));
        });

        builder.Services.AddIdentity<PortalUser, IdentityRole>(c =>
        {
            c.Password.RequireDigit = false;
            c.Password.RequireLowercase = false;
            c.Password.RequireNonAlphanumeric = false;
            c.Password.RequireUppercase = false;
            c.Password.RequiredLength = 6;
            c.Password.RequiredUniqueChars = 0;
        })
        .AddEntityFrameworkStores<QuotationsBoardContext>()
        .AddDefaultTokenProviders();

        builder.Services.AddScoped<QuotationsBoardContext>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer("Bearer", options =>
        {

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            };
        });


        // Add services to the container.
        builder.Services.AddAutoMapper(typeof(Program));
        builder.Services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNamingPolicy = null;
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();
        app.UseCors(CORS_SPECIFICATIONS);

        // JSON Serializer



        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}