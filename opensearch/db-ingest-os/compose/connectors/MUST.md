# Must do stuff 

- Once all containers are up and run, configure connectors

- The below configuration is to setup patroni-pg -> debezium - kafka 

```powershell
$body = Get-Content -Raw .\register-pg.json 
Invoke-RestMethod -Uri http://127.0.0.1:18083/connectors -Method Post -ContentType "application/json" -Headers @{ Expect = "" } -Body $body
```

```bash
curl -X POST http://127.0.0.1:18083/connectors \
  -H "Content-Type: application/json" \
  -H "Expect:" \
  --data-binary @register-pg.json
```

- The below configuration is to setup kafka-opensearch sink 

```powershell
 $body = Get-Content -Raw .\register-os-sink.json
 Invoke-RestMethod -Uri http://127.0.0.1:18083/connectors -Method Post -ContentType "application/json" -Headers @{ Expect = "" } -Body $body
 ```

 ```bash
curl -X POST http://127.0.0.1:18083/connectors \
  -H "Content-Type: application/json" \
  -H "Expect:" \
  --data-binary @register-os-sink.json
```