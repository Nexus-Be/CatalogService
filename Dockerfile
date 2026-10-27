FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8002
ENV ASPNETCORE_URLS=http://+:8002

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["Catalog.Api/Catalog.Api.csproj", "Catalog.Api/"]
COPY ["Catalog.Application/Catalog.Application.csproj", "Catalog.Application/"]
COPY ["Catalog.Domain/Catalog.Domain.csproj", "Catalog.Domain/"]
COPY ["Catalog.Infrastructure/Catalog.Infrastructure.csproj", "Catalog.Infrastructure/"]
RUN dotnet restore "Catalog.Api/Catalog.Api.csproj"
COPY . .
RUN dotnet build "Catalog.Api/Catalog.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Catalog.Api/Catalog.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Catalog.Api.dll"]
