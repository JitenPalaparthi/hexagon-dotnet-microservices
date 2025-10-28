#!/usr/bin/env bash
set -euo pipefail
export SERVICEBUS__CONNECTIONSTRING="Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
export QUEUE__NAME="orders-queue"
dotnet run --framework net9.0 --project ./consumer
