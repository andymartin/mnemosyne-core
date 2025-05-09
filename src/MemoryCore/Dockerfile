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
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/out .

# Expose port 80 for the application
EXPOSE 80

# Set the entry point to run the application
ENTRYPOINT ["dotnet", "Mnemosyne.Core.dll"]
