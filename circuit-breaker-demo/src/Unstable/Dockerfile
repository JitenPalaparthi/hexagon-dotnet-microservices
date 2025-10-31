FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY src/Unstable/*.csproj src/Unstable/
RUN dotnet restore src/Unstable/Unstable.csproj
COPY . .
RUN dotnet publish src/Unstable/Unstable.csproj -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
COPY --from=build /out ./
ENTRYPOINT ["dotnet","Unstable.dll"]
