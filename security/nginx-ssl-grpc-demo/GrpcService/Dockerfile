# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY *.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
# h2c port (no TLS here; TLS terminates at NGINX)
ENV ASPNETCORE_URLS=http://0.0.0.0:8090
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8090
ENTRYPOINT ["dotnet", "GrpcService.dll"]
