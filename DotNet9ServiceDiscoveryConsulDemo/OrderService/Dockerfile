FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore OrderService/OrderService.csproj
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /out

FROM base AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
ENV SERVICE_NAME=orders
ENV SERVICE_HOST=orders
ENV SERVICE_PORT=8080
ENV CONSUL_HTTP_ADDR=http://consul:8500
COPY --from=build /out .
ENTRYPOINT ["dotnet", "OrderService.dll"]
