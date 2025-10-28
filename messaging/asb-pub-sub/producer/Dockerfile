# build
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Producer.csproj ./
RUN dotnet restore ./Producer.csproj
COPY . .
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# run
FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine AS final
WORKDIR /app
COPY --from=build /out ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Producer.dll"]
