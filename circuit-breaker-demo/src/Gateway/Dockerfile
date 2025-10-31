FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Gateway/*.csproj src/Gateway/
RUN dotnet restore src/Gateway/Gateway.csproj
COPY . .
RUN dotnet publish src/Gateway/Gateway.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV Downstream__BaseUrl=http://unstable:8081
EXPOSE 8080
COPY --from=build /out ./
ENTRYPOINT ["dotnet","Gateway.dll"]
