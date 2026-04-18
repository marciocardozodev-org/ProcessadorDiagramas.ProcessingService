# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ProcessadorDiagramas.ProcessingService.sln ./
COPY src/ProcessadorDiagramas.ProcessingService.API/ProcessadorDiagramas.ProcessingService.API.csproj src/ProcessadorDiagramas.ProcessingService.API/
COPY src/ProcessadorDiagramas.ProcessingService.Application/ProcessadorDiagramas.ProcessingService.Application.csproj src/ProcessadorDiagramas.ProcessingService.Application/
COPY src/ProcessadorDiagramas.ProcessingService.Domain/ProcessadorDiagramas.ProcessingService.Domain.csproj src/ProcessadorDiagramas.ProcessingService.Domain/
COPY src/ProcessadorDiagramas.ProcessingService.Infrastructure/ProcessadorDiagramas.ProcessingService.Infrastructure.csproj src/ProcessadorDiagramas.ProcessingService.Infrastructure/
COPY tests/ProcessadorDiagramas.ProcessingService.Tests/ProcessadorDiagramas.ProcessingService.Tests.csproj tests/ProcessadorDiagramas.ProcessingService.Tests/

RUN dotnet restore ProcessadorDiagramas.ProcessingService.sln --verbosity minimal
RUN dotnet tool install --global dotnet-ef --version 8.0.0

ENV PATH="${PATH}:/root/.dotnet/tools"

COPY . .

RUN dotnet publish src/ProcessadorDiagramas.ProcessingService.API/ProcessadorDiagramas.ProcessingService.API.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

RUN dotnet ef migrations bundle \
    --project src/ProcessadorDiagramas.ProcessingService.Infrastructure/ProcessadorDiagramas.ProcessingService.Infrastructure.csproj \
    --startup-project src/ProcessadorDiagramas.ProcessingService.API/ProcessadorDiagramas.ProcessingService.API.csproj \
    --context ProcessadorDiagramas.ProcessingService.Infrastructure.Data.AppDbContext \
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

ENTRYPOINT ["dotnet", "ProcessadorDiagramas.ProcessingService.API.dll"]