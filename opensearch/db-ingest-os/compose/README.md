```powershell
$body = Get-Content -Raw .\connectors\register-pg.json 
Invoke-RestMethod -Uri http://127.0.0.1:18083/connectors -Method Post -ContentType "application/json" -Headers @{ Expect = "" } -Body $body
```


```powershell
$body = Get-Content -Raw .\connectors\register-os-sink.json 
Invoke-RestMethod -Uri http://127.0.0.1:18083/connectors -Method Post -ContentType "application/json" -Headers @{ Expect = "" } -Body $body
```

write simple dont api to connect to open serach and fectch data

curl -X GET "http://localhost:19200/mfdb.public.product/_search?pretty" \
  -H 'Content-Type: application/json' \
  -d '{
    "size": 100,
    "query": { "match_all": {} }
  }'



curl -X POST "http://localhost:9200/_msearch?pretty" \
  -H 'Content-Type: application/x-ndjson' \
  -d '
{{ "index": "mfdb.public.product" }
{ "query": { "match_all": {} }, "size": 5 }

{ "index": "mfdb.public.customer" }
{ "query": { "match_all": {} }, "size": 5 }}
'