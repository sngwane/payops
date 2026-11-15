using System.Text.Json.Serialization;
using PayOps.Payments.Api.Authentication;
using PayOps.Payments.Api.Endpoints;
using PayOps.Payments.Data;
using PayOps.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PayOps")
    ?? throw new InvalidOperationException("Connection string 'PayOps' is not configured (user-secrets or ConnectionStrings__PayOps).");

builder.Services.AddPaymentsData(connectionString);
builder.Services.Configure<PaymentsOptions>(builder.Configuration.GetSection(PaymentsOptions.SectionName));
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddPaymentsApiAuth(builder.Configuration, builder.Environment);
builder.Services.AddPayOpsApiDefaults().AddDbContextCheck<PaymentsDbContext>();

var app = builder.Build();

app.UsePayOpsApiDefaults();
app.MapPaymentsEndpoints();

app.Run();
