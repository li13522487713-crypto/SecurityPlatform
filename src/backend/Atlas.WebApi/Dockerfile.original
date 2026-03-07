FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/backend/Atlas.WebApi/Atlas.WebApi.csproj", "src/backend/Atlas.WebApi/"]
COPY ["src/backend/Atlas.Application/Atlas.Application.csproj", "src/backend/Atlas.Application/"]
COPY ["src/backend/Atlas.Application.Alert/Atlas.Application.Alert.csproj", "src/backend/Atlas.Application.Alert/"]
COPY ["src/backend/Atlas.Application.Approval/Atlas.Application.Approval.csproj", "src/backend/Atlas.Application.Approval/"]
COPY ["src/backend/Atlas.Application.Assets/Atlas.Application.Assets.csproj", "src/backend/Atlas.Application.Assets/"]
COPY ["src/backend/Atlas.Application.Audit/Atlas.Application.Audit.csproj", "src/backend/Atlas.Application.Audit/"]
COPY ["src/backend/Atlas.Application.Workflow/Atlas.Application.Workflow.csproj", "src/backend/Atlas.Application.Workflow/"]
COPY ["src/backend/Atlas.Infrastructure/Atlas.Infrastructure.csproj", "src/backend/Atlas.Infrastructure/"]
COPY ["src/backend/Atlas.Domain/Atlas.Domain.csproj", "src/backend/Atlas.Domain/"]
COPY ["src/backend/Atlas.Domain.Alert/Atlas.Domain.Alert.csproj", "src/backend/Atlas.Domain.Alert/"]
COPY ["src/backend/Atlas.Domain.Approval/Atlas.Domain.Approval.csproj", "src/backend/Atlas.Domain.Approval/"]
COPY ["src/backend/Atlas.Domain.Assets/Atlas.Domain.Assets.csproj", "src/backend/Atlas.Domain.Assets/"]
COPY ["src/backend/Atlas.Domain.Audit/Atlas.Domain.Audit.csproj", "src/backend/Atlas.Domain.Audit/"]
COPY ["src/backend/Atlas.Domain.Workflow/Atlas.Domain.Workflow.csproj", "src/backend/Atlas.Domain.Workflow/"]
COPY ["src/backend/Atlas.Core/Atlas.Core.csproj", "src/backend/Atlas.Core/"]
COPY ["src/backend/Atlas.WorkflowCore/Atlas.WorkflowCore.csproj", "src/backend/Atlas.WorkflowCore/"]
COPY ["src/backend/Atlas.WorkflowCore.DSL/Atlas.WorkflowCore.DSL.csproj", "src/backend/Atlas.WorkflowCore.DSL/"]

RUN dotnet restore "src/backend/Atlas.WebApi/Atlas.WebApi.csproj"

COPY . .
RUN dotnet publish "src/backend/Atlas.WebApi/Atlas.WebApi.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 5000
HEALTHCHECK --interval=10s --timeout=3s --start-period=20s --retries=12 CMD curl -fsS http://127.0.0.1:5000/api/v1/health || exit 1

ENTRYPOINT ["dotnet", "Atlas.WebApi.dll"]
