using System.Text.Json.Serialization;
using PayOps.BatchFile.Api.Authentication;
using PayOps.BatchFile.Api.Endpoints;
using PayOps.BatchFile.Data;
using PayOps.Client.Data;
using PayOps.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PayOps")
    ?? throw new InvalidOperationException("Connection string 'PayOps' is not configured (user-secrets or ConnectionStrings__PayOps).");

builder.Services.AddBatchFileData(connectionString);
builder.Services.Configure<BatchFileOptions>(builder.Configuration.GetSection(BatchFileOptions.SectionName));
builder.Services.AddClientData(connectionString);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddBatchFileAuth(builder.Configuration, builder.Environment);
builder.Services.AddPayOpsApiDefaults(o => o.AddApiKeySecurityScheme()).AddDbContextCheck<BatchFileDbContext>();

var app = builder.Build();

app.UsePayOpsApiDefaults();
app.MapBatchEndpoints();
app.MapStaffEndpoints();

app.Run();
