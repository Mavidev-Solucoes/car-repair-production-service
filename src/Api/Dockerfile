FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/Api/Api.csproj", "src/Api/"]
COPY ["src/Application/Application.csproj", "src/Application/"]
COPY ["src/Domain/Domain.csproj", "src/Domain/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "src/Infrastructure/"]
RUN dotnet restore "src/Api/Api.csproj"
COPY . .
WORKDIR "/src/src/Api"
RUN dotnet build "Api.csproj" -c Release -o /app/build

FROM build AS publish
WORKDIR "/src/src/Api"
RUN dotnet publish "Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Agente .NET do New Relic (o profiler só liga com CORECLR_ENABLE_PROFILING=1, definido no k8s).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS newrelic
ARG TARGETARCH
ARG NEW_RELIC_AGENT_VERSION=10.57.0
RUN arch="${TARGETARCH:-amd64}" \
    && curl -fsSL -o /tmp/newrelic-agent.deb \
       "https://download.newrelic.com/dot_net_agent/previous_releases/${NEW_RELIC_AGENT_VERSION}/newrelic-dotnet-agent_${NEW_RELIC_AGENT_VERSION}_${arch}.deb" \
    && dpkg-deb -x /tmp/newrelic-agent.deb /newrelic \
    && rm /tmp/newrelic-agent.deb

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=newrelic /newrelic/usr/local/newrelic-dotnet-agent /usr/local/newrelic-dotnet-agent
ENV CORECLR_ENABLE_PROFILING=0 \
    CORECLR_PROFILER={36032161-FFC0-4B61-B559-F6C5D41BAE5A} \
    CORECLR_NEWRELIC_HOME=/usr/local/newrelic-dotnet-agent \
    CORECLR_PROFILER_PATH=/usr/local/newrelic-dotnet-agent/libNewRelicProfiler.so \
    NEW_RELIC_DISTRIBUTED_TRACING_ENABLED=true
ENTRYPOINT ["dotnet", "Api.dll"]
