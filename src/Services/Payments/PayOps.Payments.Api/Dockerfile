# Build from the repo root: docker build -f src/Services/Payments/PayOps.Payments.Api/Dockerfile -t payops-payments-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Shared/PayOps.Database/PayOps.Database.csproj src/Shared/PayOps.Database/
COPY src/Shared/PayOps.ServiceDefaults/PayOps.ServiceDefaults.csproj src/Shared/PayOps.ServiceDefaults/
COPY src/Services/Payments/PayOps.Payments.Data/PayOps.Payments.Data.csproj src/Services/Payments/PayOps.Payments.Data/
COPY src/Services/Payments/PayOps.Payments.Api/PayOps.Payments.Api.csproj src/Services/Payments/PayOps.Payments.Api/
RUN dotnet restore src/Services/Payments/PayOps.Payments.Api/PayOps.Payments.Api.csproj
COPY src/Shared/ src/Shared/
COPY src/Services/Payments/ src/Services/Payments/
RUN dotnet publish src/Services/Payments/PayOps.Payments.Api/PayOps.Payments.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayOps.Payments.Api.dll"]
