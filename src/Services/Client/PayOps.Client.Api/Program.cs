using PayOps.Client.Api.Authentication;
using PayOps.Client.Api.Endpoints;
using PayOps.Client.Data;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("PayOps")
    ?? throw new InvalidOperationException("Connection string 'PayOps' is not configured (user-secrets or ConnectionStrings__PayOps).");

builder.Services.AddClientData(connectionString);
builder.Services.AddClientApiAuth(builder.Configuration, builder.Environment);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks().AddDbContextCheck<ClientDbContext>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health").AllowAnonymous();
app.MapClientEndpoints();

app.Run();

public partial class Program;
