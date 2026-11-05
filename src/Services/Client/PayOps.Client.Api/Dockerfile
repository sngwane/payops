# Build from the repo root: docker build -f src/Services/Client/PayOps.Client.Api/Dockerfile -t payops-client-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Shared/PayOps.Database/PayOps.Database.csproj src/Shared/PayOps.Database/
COPY src/Services/Client/PayOps.Client.Data/PayOps.Client.Data.csproj src/Services/Client/PayOps.Client.Data/
COPY src/Services/Client/PayOps.Client.Api/PayOps.Client.Api.csproj src/Services/Client/PayOps.Client.Api/
RUN dotnet restore src/Services/Client/PayOps.Client.Api/PayOps.Client.Api.csproj
COPY src/Shared/PayOps.Database/ src/Shared/PayOps.Database/
COPY src/Services/Client/ src/Services/Client/
RUN dotnet publish src/Services/Client/PayOps.Client.Api/PayOps.Client.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayOps.Client.Api.dll"]
