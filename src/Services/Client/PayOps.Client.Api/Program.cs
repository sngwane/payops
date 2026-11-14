using PayOps.Client.Api.Authentication;
using PayOps.Client.Api.Endpoints;
using PayOps.Client.Data;
using PayOps.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PayOps")
    ?? throw new InvalidOperationException("Connection string 'PayOps' is not configured (user-secrets or ConnectionStrings__PayOps).");

builder.Services.AddClientData(connectionString);
builder.Services.AddClientApiAuth(builder.Configuration, builder.Environment);
builder.Services.AddPayOpsApiDefaults().AddDbContextCheck<ClientDbContext>();

var app = builder.Build();

app.UsePayOpsApiDefaults();
app.MapClientEndpoints();

app.Run();

public partial class Program;
