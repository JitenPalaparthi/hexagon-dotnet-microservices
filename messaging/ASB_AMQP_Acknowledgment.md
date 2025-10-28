
# How Consumers Acknowledge Messages in Azure Service Bus (AMQP)

This document explains how a **consumer** informs Azure Service Bus (ASB) that a message has been successfully processed, focusing on **AMQP-level acknowledgment**.

---

## 🧭 Message Lifecycle Overview

1. **Producer sends** a message → ASB stores it durably.  
2. **Consumer receives** the message (via AMQP).  
3. **Broker locks** the message to prevent other consumers from processing it.  
4. **Consumer processes** the message.  
5. **Consumer sends ACK** (Complete) via AMQP.  
6. **ASB removes** the message permanently.  
7. If ACK is not received before the lock expires → message is **redelivered**.

---

## ⚙️ Receive Modes in Azure Service Bus

| Mode | Description | Reliability |
|------|--------------|-------------|
| **Peek-Lock** | Message is locked until consumer explicitly completes (ACKs) it. | ✅ Reliable |
| **Receive-and-Delete** | Message is deleted immediately when delivered. | ⚠️ Not reliable |

---

### 🔒 Peek-Lock Mode (Default)

1. Consumer receives message → message is **locked**.  
2. The lock duration (default 30s) ensures **exclusive access**.  
3. Consumer does work (e.g., write to DB).  
4. If successful → **ACK** (Complete).  
5. If failed → **Abandon** (unlock), **Defer**, or **Dead-letter**.

**Python Example:**

```python
receiver = client.get_queue_receiver("orders", receive_mode=ServiceBusReceiveMode.PEEK_LOCK)
async with receiver:
    async for msg in receiver:
        try:
            process(msg)
            await receiver.complete_message(msg)  # ✅ ACK (Accepted state)
        except Exception:
            await receiver.abandon_message(msg)  # ❌ Re-queued
```

---

### ⚡ Receive-and-Delete Mode

Message is **removed immediately** upon delivery — no ACKs.

```python
receiver = client.get_queue_receiver("orders", receive_mode=ServiceBusReceiveMode.RECEIVE_AND_DELETE)
async with receiver:
    async for msg in receiver:
        process(msg)  # message already deleted
```

---

## 🧱 What Happens at the AMQP Protocol Level

At the wire level, AMQP uses a **Disposition frame** to mark message outcomes:

| SDK Action | AMQP State | Description |
|-------------|-------------|-------------|
| `complete_message()` | `Accepted` | ✅ Message processed successfully and deleted |
| `abandon_message()` | `Released` | 🔁 Message unlocked and returned to queue |
| `defer_message()` | `Modified` | ⏸ Message deferred until explicitly fetched |
| `dead_letter_message()` | `Rejected` | ☠️ Moved to Dead-letter Queue |

**Example (AMQP Disposition Frame):**
```
Role: receiver
First: delivery-id
Last: delivery-id
State: accepted
Settled: true
```

---

## ⏳ Lock Renewal

If message processing exceeds lock timeout, consumer must **renew lock** to prevent re-delivery.

```python
await receiver.renew_message_lock(msg)
```

This sends an AMQP control message to refresh the lock duration.

---

## ☠️ Dead-Letter Queue (DLQ)

Messages go to the DLQ when:
- Delivery count exceeds max retries.  
- Lock expires repeatedly.  
- Consumer explicitly dead-letters.  
- TTL (Time-To-Live) expires.

---

## 📊 Summary Table

| Action | SDK Method | AMQP State | Broker Behavior |
|---------|-------------|-------------|----------------|
| **Complete** | `complete_message()` | `Accepted` | Message deleted |
| **Abandon** | `abandon_message()` | `Released` | Message unlocked |
| **Defer** | `defer_message()` | `Modified` | Message hidden |
| **Dead-letter** | `dead_letter_message()` | `Rejected` | Moved to DLQ |
| **Receive-and-Delete** | Implicit | `Accepted (auto)` | Message deleted instantly |

---

## 💡 Summary

- In **Peek-Lock**, consumer **must send ACK** (`Accepted` disposition).  
- In **Receive-and-Delete**, the broker assumes success automatically.  
- The **ACK** is a low-level **AMQP Disposition frame** — ASB deletes the message once received.  
- **Lock renewal** and **dead-lettering** ensure reliability and recovery.

---

**In Simple Terms:**  
> When a consumer calls `complete_message()`, the AMQP client sends a **Disposition frame** with `state=accepted` to the Azure Service Bus broker.  
> The broker then **permanently removes the message**, confirming successful consumption.

---

*Author: Jiten Palaparthi*  
*Topic: AMQP Internals in Azure Service Bus*
