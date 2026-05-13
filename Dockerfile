# ---- Build aşaması ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/K8sPlayground.Web/K8sPlayground.Web.csproj ./K8sPlayground.Web/
RUN dotnet restore K8sPlayground.Web/K8sPlayground.Web.csproj
COPY src/K8sPlayground.Web/. ./K8sPlayground.Web/
RUN dotnet publish K8sPlayground.Web/K8sPlayground.Web.csproj \
    -c Release -o /app /p:UseAppHost=false

# ---- Runtime aşaması ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0

# Non-root kullanıcıyı ve dizinleri ÖNCE oluştur — sonra dosyalar doğru sahiple
# kopyalansın. Aksi hâlde /app dosyaları root'a ait kalır ve UID 10001 onları
# okuyamaz (UnauthorizedAccessException: Access to /app/appsettings.json denied).
RUN useradd -u 10001 -m appuser \
 && mkdir -p /app /data \
 && chown -R 10001:10001 /app /data \
 && chmod 0775 /data

WORKDIR /app
COPY --chown=10001:10001 --from=build /app .

# Eğitim için 8080 portunda dinle
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

USER 10001

EXPOSE 8080
ENTRYPOINT ["dotnet", "K8sPlayground.Web.dll"]
