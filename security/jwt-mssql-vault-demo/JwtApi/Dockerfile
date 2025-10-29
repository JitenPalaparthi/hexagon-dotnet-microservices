FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY JwtApi.csproj ./
RUN dotnet restore
COPY . ./
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8088
ENV ASPNETCORE_URLS=http://0.0.0.0:8088
ENTRYPOINT ["dotnet", "JwtApi.dll"]
