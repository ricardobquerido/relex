# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY ["Relex.slnx", "./"]
COPY ["Relex.Api/Relex.Api.csproj", "Relex.Api/"]
COPY ["Relex.Domain/Relex.Domain.csproj", "Relex.Domain/"]
COPY ["Relex.Tests/Relex.Tests.csproj", "Relex.Tests/"]

# Restore dependencies
RUN dotnet restore "Relex.Api/Relex.Api.csproj"

# Copy source code
COPY . .

# Build and Publish
WORKDIR "/src/Relex.Api"
RUN dotnet publish "Relex.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime Stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Relex.Api.dll"]
