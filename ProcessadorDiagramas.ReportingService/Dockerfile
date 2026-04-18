# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProcessadorDiagramas.ReportingService.sln ./
COPY src/ProcessadorDiagramas.ReportingService.API/ProcessadorDiagramas.ReportingService.API.csproj src/ProcessadorDiagramas.ReportingService.API/
COPY src/ProcessadorDiagramas.ReportingService.Application/ProcessadorDiagramas.ReportingService.Application.csproj src/ProcessadorDiagramas.ReportingService.Application/
COPY src/ProcessadorDiagramas.ReportingService.Domain/ProcessadorDiagramas.ReportingService.Domain.csproj src/ProcessadorDiagramas.ReportingService.Domain/
COPY src/ProcessadorDiagramas.ReportingService.Infrastructure/ProcessadorDiagramas.ReportingService.Infrastructure.csproj src/ProcessadorDiagramas.ReportingService.Infrastructure/
COPY tests/ProcessadorDiagramas.ReportingService.Tests/ProcessadorDiagramas.ReportingService.Tests.csproj tests/ProcessadorDiagramas.ReportingService.Tests/

RUN dotnet restore ProcessadorDiagramas.ReportingService.sln --verbosity minimal
RUN dotnet tool install --global dotnet-ef --version 8.0.0

ENV PATH="${PATH}:/root/.dotnet/tools"

COPY . .

RUN dotnet publish src/ProcessadorDiagramas.ReportingService.API/ProcessadorDiagramas.ReportingService.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

RUN dotnet ef migrations bundle \
    --project src/ProcessadorDiagramas.ReportingService.Infrastructure/ProcessadorDiagramas.ReportingService.Infrastructure.csproj \
    --startup-project src/ProcessadorDiagramas.ReportingService.API/ProcessadorDiagramas.ReportingService.API.csproj \
    --context ProcessadorDiagramas.ReportingService.Infrastructure.Data.AppDbContext \
    --configuration Release \
    --self-contained \
    --runtime linux-x64 \
    --output /app/efbundle

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .
COPY --from=build /app/efbundle /app/efbundle

ENTRYPOINT ["dotnet", "ProcessadorDiagramas.ReportingService.API.dll"]
