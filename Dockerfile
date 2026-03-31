# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files for layer caching
COPY Flowmetry.sln ./
COPY Flowmetry.API/Flowmetry.API.csproj Flowmetry.API/
COPY Flowmetry.Application/Flowmetry.Application.csproj Flowmetry.Application/
COPY Flowmetry.Infrastructure/Flowmetry.Infrastructure.csproj Flowmetry.Infrastructure/
COPY Flowmetry.Domain/Flowmetry.Domain.csproj Flowmetry.Domain/
COPY Flowmetry.API.Tests/Flowmetry.API.Tests.csproj Flowmetry.API.Tests/

# Restore dependencies
RUN dotnet restore

# Copy all source files
COPY . .

# Publish the API project
RUN dotnet publish Flowmetry.API/Flowmetry.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Flowmetry.API.dll"]
