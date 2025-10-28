### Run Producer

```bash

export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export TOPIC__NAME=demo-topic
dotnet run --framework net9.0 --project ./producer
```

### Run Subscriber-A 

```bash
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export TOPIC__NAME=demo-topic
export SUBSCRIPTION__NAME=sub-a
dotnet run --framework net9.0 --project ./consumer
```

### Run Subscriber-A 

```bash
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export TOPIC__NAME=demo-topic
export SUBSCRIPTION__NAME=sub-b
dotnet run --framework net9.0 --project ./consumer
```

### pub data

# with Producer running (TOPIC__NAME set)
curl -X POST http://localhost:5000/send \
  -H "content-type: application/json" \
  -d '{"text":"hello pubsub"}'


  ### Concurrency 

export PROCESSOR__MAXCONCURRENT=8
export PROCESSOR__PREFETCH=50
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export TOPIC__NAME=demo-topic
export SUBSCRIPTION__NAME=sub-a
dotnet run --framework net9.0 --project ./consumer
  