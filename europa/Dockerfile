# Base image for runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["europa.csproj", "./"]
RUN dotnet restore "europa.csproj"
COPY . .
RUN dotnet build "europa.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "europa.csproj" -c Release -o /app/publish

# Final stage
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "europa.dll"]