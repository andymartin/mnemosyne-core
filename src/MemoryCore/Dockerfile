# Use the official .NET SDK image to build the app
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build-env
WORKDIR /app

# Copy project file and restore dependencies
COPY Mnemosyne.Core.csproj ./
RUN dotnet restore

# Copy all the project files and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# Install curl for health checks
RUN apt-get update && apt-get install -y curl && rm -rf /var/lib/apt/lists/*

# ENV for better diagnostics (used by Visual Studio)
ENV DOTNET_USE_POLLING_FILE_WATCHER=true \
    ASPNETCORE_URLS=http://+:80 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
   
# Debugger support
RUN apt-get update && \
    apt-get install -y unzip && \
    curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

COPY --from=build-env /app/out .

# Expose port 80 for the application
EXPOSE 80

# Set environment variable to use port 80
ENV ASPNETCORE_URLS=http://+:80

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "Mnemosyne.Core.dll"]
