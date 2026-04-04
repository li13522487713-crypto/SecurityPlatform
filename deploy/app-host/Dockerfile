FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

RUN apt-get update && apt-get install -y --no-install-recommends unzip \
    && rm -rf /var/lib/apt/lists/*

ARG APP_PACKAGE=app-package.zip
COPY ${APP_PACKAGE} /tmp/app-package.zip
RUN unzip -oq /tmp/app-package.zip -d /app/instance \
    && rm /tmp/app-package.zip

ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100

HEALTHCHECK --interval=10s --timeout=3s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:5100/health/live || exit 1

ENTRYPOINT ["/app/instance/backend/Atlas.AppHost"]
