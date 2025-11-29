using Core.Helpers;
using Core.Schedulers;
using Core.Security;
using Core.Services;
using Core.Services.ConnectionTesting;
using Core.Services.Credential;
using Core.Services.ExecutionManagement;
using Core.Services.FileOperations;
using Core.Services.Login;
using Core.Services.PatternProcessor;
using Core.Services.TasksManagement;
using Core.Utils;
using Data.Context;
using Data.Interfaces;
using Data.Models;
using Data.Repositories;
using Mapster;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", builder =>
    {
        builder.WithOrigins("*")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var cnnString = builder.Configuration.GetConnectionString("DbConnString");
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(cnnString));

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

//repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<ILoginAttemptRepository, LoginAttemptRepository>();
builder.Services.AddScoped<IFileTransferTaskRepository, FileTransferTaskRepository>();
builder.Services.AddScoped<ITransferTimeSlotRepository, TransferTimeSlotRepository>();
builder.Services.AddScoped<IServerCredentialRepository, ServerCredentialRepository>();

builder.Services.AddScoped<IRepository<TransferExecution>, Repository<TransferExecution>>();
builder.Services.AddScoped<IRepository<TransferredFile>, Repository<TransferredFile>>();

//services
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ILoginAttemptService, LoginAttemptService>();
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddSingleton<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<ITasksManagementService, TasksManagementService>();
builder.Services.AddScoped<IExecutionManagementService, ExecutionManagementService>();
builder.Services.AddScoped<IConnectionTestingService, ConnectionTestingService>();
builder.Services.AddScoped<IFileOperationsService, FileOperationsService>();
builder.Services.AddScoped<IServerCredential, ServerCrediential>();
builder.Services.AddScoped<IPatternProcessorService, PatternProcessorService>();


// Register background scheduler
builder.Services.AddHostedService<FileTransferScheduler>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options => {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!))
        };
    });

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme()
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

bool seedData = builder.Configuration.GetValue<bool>("SeedDatabase:Value", false);

if (seedData)
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userService = scope.ServiceProvider.GetRequiredService<IUserService>();
        dbContext.Database.EnsureCreated();
        DatabaseSeeder.SeedAdminUser(dbContext, userService);
    }
}

MapsterConfig.Configure();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("CorsPolicy");

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();


app.Run();