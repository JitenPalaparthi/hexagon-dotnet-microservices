Emulator & entity
	•	docker-compose.yml starts SQL Server + Service Bus Emulator.
	•	emulator/config.json creates a queue orders-queue with:
	•	RequiresSession=true → enables per-order FIFO using SessionId.
	•	MaxDeliveryCount=3 → after 3 failed deliveries, message goes to DLQ.
	•	DefaultMessageTimeToLive="PT30M" → messages expire after 30 minutes.
	•	DeadLetteringOnMessageExpiration=true → expired messages also go to DLQ.

Producer (Minimal API)
	•	Endpoint: POST /send-order?orderId=<id>&steps=N&repeat=M
	•	For each sequence, it sends steps messages with:
	•	SessionId = orderId → this is the ordering key.
	•	MessageId unique (handy if you turn on duplicate detection later).
	•	So for order-123 and steps=3, it emits: Step1 → Step2 → Step3 (in order) to orders-queue.

Consumer (Worker) — session-aware
	•	Uses ServiceBusSessionProcessor:
	•	MaxConcurrentCallsPerSession = 1 → preserves FIFO inside a session (per order).
	•	MaxConcurrentSessions (env: MAX_CONCURRENT_SESSIONS) → processes many orders in parallel.
	•	PrefetchCount (env: PREFETCH) → buffers messages locally for throughput.
	•	Optional failure injection (env: SIM_FAIL_STEP):
	•	If set to e.g. 2, every message with "step":2 will throw, causing retries up to 3 then DLQ.

DLQ Reader
	•	Reads from orders-queue/$DeadLetterQueue.
	•	Prints DeadLetterReason, DeadLetterErrorDescription, SessionId, body.
	•	If DLQ_REQUEUE=1, it requeues the DLQ message back to the active queue (same SessionId), so it can be reprocessed.