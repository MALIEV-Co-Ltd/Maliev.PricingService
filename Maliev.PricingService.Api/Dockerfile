FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app
EXPOSE 8080
EXPOSE 8443

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Maliev.PricingService/Maliev.PricingService.Api/Maliev.PricingService.Api.csproj", "Maliev.PricingService/Maliev.PricingService.Api/"]
COPY ["Maliev.PricingService/Maliev.PricingService.Data/Maliev.PricingService.Data.csproj", "Maliev.PricingService/Maliev.PricingService.Data/"]
COPY ["Maliev.Aspire/Maliev.Aspire.ServiceDefaults/Maliev.Aspire.ServiceDefaults.csproj", "Maliev.Aspire/Maliev.Aspire.ServiceDefaults/"]
COPY ["Maliev.MessagingContracts/generated/csharp/Maliev.MessagingContracts.csproj", "Maliev.MessagingContracts/generated/csharp/"]
RUN dotnet restore "./Maliev.PricingService/Maliev.PricingService.Api/Maliev.PricingService.Api.csproj"
COPY . .
WORKDIR "/src/Maliev.PricingService/Maliev.PricingService.Api"
RUN dotnet build "./Maliev.PricingService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./Maliev.PricingService.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Maliev.PricingService.Api.dll"]
