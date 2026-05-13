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
WORKDIR /app
COPY --from=build /app .

# Eğitim için 8080 portunda dinle
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_RUNNING_IN_CONTAINER=true

# Veri dizini (PVC mount edilmezse boş kalır, emptyDir gibi davranır)
RUN mkdir -p /data && chmod 0777 /data

# Root olmadan çalış
RUN useradd -u 10001 -m appuser
USER 10001

EXPOSE 8080
ENTRYPOINT ["dotnet", "K8sPlayground.Web.dll"]
