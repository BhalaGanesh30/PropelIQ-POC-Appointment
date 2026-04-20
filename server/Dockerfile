# ASP.NET Core 8 development Dockerfile — dotnet watch run with auto-recompile.
# Used exclusively for local development via Docker Compose.
# Production uses a separate multi-stage Dockerfile (added in EP-007).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS dev
WORKDIR /src

# Restore dependencies first for Docker layer caching — re-runs only when
# .csproj or .sln files change, not on every source edit.
COPY PropelIQ.sln ./
COPY src/PropelIQ.Api/PropelIQ.Api.csproj src/PropelIQ.Api/
COPY src/PropelIQ.SharedKernel/PropelIQ.SharedKernel.csproj src/PropelIQ.SharedKernel/
COPY src/Modules/ src/Modules/

RUN dotnet restore PropelIQ.sln

# Copy remaining source files.
COPY . .

EXPOSE 5000

# ASPNETCORE_URLS: bind to all interfaces on port 5000 (no HTTPS in dev container).
# DOTNET_USE_POLLING_FILE_WATCHER: required for Docker volume mounts on Windows/macOS.
ENV ASPNETCORE_URLS=http://+:5000
ENV DOTNET_USE_POLLING_FILE_WATCHER=true

WORKDIR /src/src/PropelIQ.Api
# dotnet watch run restarts the process on every saved .cs file change.
CMD ["dotnet", "watch", "run", "--no-launch-profile"]
