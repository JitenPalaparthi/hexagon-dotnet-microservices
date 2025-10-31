# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/OtelMetricsDemo/*.csproj src/OtelMetricsDemo/
RUN dotnet restore src/OtelMetricsDemo/OtelMetricsDemo.csproj
COPY . .
RUN dotnet publish src/OtelMetricsDemo/OtelMetricsDemo.csproj -c Release -o /out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /out ./
ENTRYPOINT ["dotnet","OtelMetricsDemo.dll"]
