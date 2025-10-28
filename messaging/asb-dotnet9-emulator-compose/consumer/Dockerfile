# build
FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Consumer.csproj ./
RUN dotnet restore ./Consumer.csproj
COPY . .
RUN dotnet publish -c Release -o /out /p:UseAppHost=false

# run
FROM mcr.microsoft.com/dotnet/runtime:9.0-alpine AS final
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Consumer.dll"]
