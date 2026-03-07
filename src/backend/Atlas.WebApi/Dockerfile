# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/backend/Atlas.WebApi/Atlas.WebApi.csproj", "src/backend/Atlas.WebApi/"]
COPY ["src/backend/Atlas.Application/Atlas.Application.csproj", "src/backend/Atlas.Application/"]
COPY ["src/backend/Atlas.Core/Atlas.Core.csproj", "src/backend/Atlas.Core/"]
COPY ["src/backend/Atlas.Domain/Atlas.Domain.csproj", "src/backend/Atlas.Domain/"]
COPY ["src/backend/Atlas.Application.Audit/Atlas.Application.Audit.csproj", "src/backend/Atlas.Application.Audit/"]
COPY ["src/backend/Atlas.Domain.Audit/Atlas.Domain.Audit.csproj", "src/backend/Atlas.Domain.Audit/"]
COPY ["src/backend/Atlas.Domain.Approval/Atlas.Domain.Approval.csproj", "src/backend/Atlas.Domain.Approval/"]
COPY ["src/backend/Atlas.Application.Alert/Atlas.Application.Alert.csproj", "src/backend/Atlas.Application.Alert/"]
COPY ["src/backend/Atlas.Domain.Alert/Atlas.Domain.Alert.csproj", "src/backend/Atlas.Domain.Alert/"]
COPY ["src/backend/Atlas.Application.Approval/Atlas.Application.Approval.csproj", "src/backend/Atlas.Application.Approval/"]
COPY ["src/backend/Atlas.Application.Assets/Atlas.Application.Assets.csproj", "src/backend/Atlas.Application.Assets/"]
COPY ["src/backend/Atlas.Domain.Assets/Atlas.Domain.Assets.csproj", "src/backend/Atlas.Domain.Assets/"]
COPY ["src/backend/Atlas.Infrastructure/Atlas.Infrastructure.csproj", "src/backend/Atlas.Infrastructure/"]
COPY ["src/backend/Atlas.Domain.Workflow/Atlas.Domain.Workflow.csproj", "src/backend/Atlas.Domain.Workflow/"]
COPY ["src/backend/Atlas.WorkflowCore/Atlas.WorkflowCore.csproj", "src/backend/Atlas.WorkflowCore/"]
COPY ["src/backend/Atlas.WorkflowCore.DSL/Atlas.WorkflowCore.DSL.csproj", "src/backend/Atlas.WorkflowCore.DSL/"]
RUN dotnet restore "./src/backend/Atlas.WebApi/Atlas.WebApi.csproj"
COPY . .
WORKDIR "/src/src/backend/Atlas.WebApi"
RUN dotnet build "./Atlas.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Atlas.WebApi.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Atlas.WebApi.dll"]