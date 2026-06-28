FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/LlmUtilityApi/LlmUtilityApi.csproj src/LlmUtilityApi/
RUN dotnet restore src/LlmUtilityApi/LlmUtilityApi.csproj
COPY src/LlmUtilityApi/ src/LlmUtilityApi/
RUN dotnet publish src/LlmUtilityApi/LlmUtilityApi.csproj -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl \
 && rm -rf /var/lib/apt/lists/*
RUN mkdir -p /app/bin
COPY --from=build /out /app/bin
WORKDIR /app
ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_CONTENTROOT=/app/bin \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0
EXPOSE 8080
ENTRYPOINT ["dotnet", "/app/bin/LlmUtilityApi.dll"]
