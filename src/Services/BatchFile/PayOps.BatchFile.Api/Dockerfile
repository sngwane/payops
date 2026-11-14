# Build from the repo root: docker build -f src/Services/BatchFile/PayOps.BatchFile.Api/Dockerfile -t payops-batchfile-api .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Build.props ./
COPY src/Shared/PayOps.Database/PayOps.Database.csproj src/Shared/PayOps.Database/
COPY src/Shared/PayOps.ServiceDefaults/PayOps.ServiceDefaults.csproj src/Shared/PayOps.ServiceDefaults/
COPY src/Services/Client/PayOps.Client.Data/PayOps.Client.Data.csproj src/Services/Client/PayOps.Client.Data/
COPY src/Services/BatchFile/PayOps.BatchFile.Data/PayOps.BatchFile.Data.csproj src/Services/BatchFile/PayOps.BatchFile.Data/
COPY src/Services/BatchFile/PayOps.BatchFile.Api/PayOps.BatchFile.Api.csproj src/Services/BatchFile/PayOps.BatchFile.Api/
RUN dotnet restore src/Services/BatchFile/PayOps.BatchFile.Api/PayOps.BatchFile.Api.csproj
COPY src/Shared/ src/Shared/
COPY src/Services/Client/PayOps.Client.Data/ src/Services/Client/PayOps.Client.Data/
COPY src/Services/BatchFile/ src/Services/BatchFile/
RUN dotnet publish src/Services/BatchFile/PayOps.BatchFile.Api/PayOps.BatchFile.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "PayOps.BatchFile.Api.dll"]
