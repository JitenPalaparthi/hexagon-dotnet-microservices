# from GrpcTlsServer/
dotnet restore
dotnet run
# or specify your own:
TLS_PFX_PATH=certs/localhost.pfx TLS_PFX_PASSWORD=changeit PORT=5001 dotnet run