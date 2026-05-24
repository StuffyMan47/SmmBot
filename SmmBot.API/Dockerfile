FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["SmmBot.API/SmmBot.API.csproj", "SmmBot.API/"]
COPY ["SmmBot.Core/SmmBot.Core.csproj", "SmmBot.Core/"]
COPY ["SmmBot.Infrastructure/SmmBot.Infrastructure.csproj", "SmmBot.Infrastructure/"]
COPY ["SmmBot.Bot/SmmBot.Bot.csproj", "SmmBot.Bot/"]
RUN dotnet restore "SmmBot.API/SmmBot.API.csproj"
COPY . .
WORKDIR "/src/SmmBot.API"
RUN dotnet build "SmmBot.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "SmmBot.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmmBot.API.dll"]
