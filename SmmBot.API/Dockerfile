FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8082
EXPOSE 8083

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
ENV DOTNET_NUGET_SIGNATURE_VERIFICATION=false
WORKDIR /src
COPY ["SmmBot.API/SmmBot.API.csproj", "SmmBot.API/"]
COPY ["SmmBot.Core/SmmBot.Core.csproj", "SmmBot.Core/"]
COPY ["SmmBot.Infrastructure/SmmBot.Infrastructure.csproj", "SmmBot.Infrastructure/"]
COPY ["SmmBot.Bot/SmmBot.Bot.csproj", "SmmBot.Bot/"]
RUN dotnet restore "SmmBot.API/SmmBot.API.csproj"
COPY . .
COPY SmmBot.Infrastructure/PhotoReferences ./PhotoReferences/
WORKDIR "/src/SmmBot.API"
RUN dotnet build --no-restore "SmmBot.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish --no-restore "SmmBot.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app

# ==========================================
# НАЧАЛО: Установка доверенных сертификатов
# ==========================================
USER root

# Копируем все 5 сертификатов в системную папку доверенных сертификатов
COPY SmmBot.API/Certs/russian_trusted_root_ca_gost_2025_pem.crt /usr/local/share/ca-certificates/russian_trusted_root_ca_gost_2025_pem.crt
COPY SmmBot.API/Certs/russian_trusted_root_ca_pem.crt /usr/local/share/ca-certificates/russian_trusted_root_ca_pem.crt
COPY SmmBot.API/Certs/russian_trusted_sub_ca_2024_pem.crt /usr/local/share/ca-certificates/russian_trusted_sub_ca_2024_pem.crt
COPY SmmBot.API/Certs/russian_trusted_sub_ca_gost_2025_pem.crt /usr/local/share/ca-certificates/russian_trusted_sub_ca_gost_2025_pem.crt
COPY SmmBot.API/Certs/russian_trusted_sub_ca_pem.crt /usr/local/share/ca-certificates/russian_trusted_sub_ca_pem.crt

# Обновляем хранилище сертификатов (создаст хеши в /etc/ssl/certs/)
RUN update-ca-certificates

# Возвращаем безопасного пользователя для запуска приложения
USER $APP_UID
# ==========================================
# КОНЕЦ: Установка доверенных сертификатов
# ==========================================


COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "SmmBot.API.dll"]
