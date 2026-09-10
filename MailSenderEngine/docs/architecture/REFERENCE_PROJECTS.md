# MailSenderEngine — Reference Projects & Architecture Decisions

## Purpose

This document records external open-source projects inspected as architectural references for MailSenderEngine.

References are used to understand proven mechanisms, not to copy implementations blindly.

The target is a legitimate, policy-compliant SMTP delivery engine.

## Reference areas

| Area | Primary reference | What we inspect |
|---|---|---|
| SMTP session | MailKit | persistent connection, capabilities, responses, reconnect, cancellation |
| MIME/message construction | MimeKit | message model and serialization |
| Worker lifecycle | .NET Worker/hosting patterns | bounded concurrency, startup/shutdown |
| Retry | .NET resilience patterns | classification, backoff, jitter, retry limits |
| Rate limiting | .NET rate-limiting patterns | global/provider/domain limits and recovery |
| Durable queue | .NET queue/storage projects | claim/lease, persistence, restart recovery, idempotency |
| Observability | .NET logging/metrics patterns | structured events, health and diagnostics |
| Bulk SMTP delivery | selected open-source mail senders | real-world session/queue/throttling trade-offs |

## Architecture target

The MailSenderEngine will evolve toward this topology:

```text
                         ┌─ SMTP session A ─ domain X
                         │
Queue → Scheduler ───────┼─ SMTP session B ─ domain X
                         │
                         ├─ SMTP session C ─ domain Y
                         │
                         └─ SMTP session D ─ domain Z
```

### Responsibilities

**Queue**
- Owns durable message state.
- Makes work available without assigning SMTP connections directly.
- Supports retry scheduling, attempts, leases and restart recovery when implemented.

**Scheduler**
- Selects eligible queued messages.
- Groups/routes work according to provider/domain/session constraints.
- Applies scheduling decisions but does not implement SMTP protocol details.

**Worker/session**
- Owns exactly one SMTP session at a time.
- A `MailKit.Net.Smtp.SmtpClient` instance is never concurrently used by multiple workers.
- Sends multiple messages through the same connected session when the provider permits it.
- Detects connection loss and coordinates safe reconnect through the retry/session policy.

**Domain routing**
- Recipient domain is a scheduling/rate-limit dimension, not a reason to duplicate message-generation logic.
- Multiple sessions may serve the same domain when concurrency limits justify it.
- Different domains may have independent limits and recovery state.

**Rate limiting**
- Global, provider and per-domain limits remain separate from worker concurrency.
- Server responses may influence adaptive throttling, but the system must not attempt to bypass provider controls.

## Evolution by vertical slices

### Slice 1 — one message

```text
SingleMailSender
  → SMTP session
  → Connect
  → Authenticate
  → Send
  → Disconnect
```

### Slice 2 — ten messages

```text
Scheduler
   → Worker
      → persistent SMTP session
         → message 1
         → message 2
         → ...
         → message 10
```

This slice introduces the minimal SMTP error classifier:

- `4xx` / temporary → reconnect/retry according to bounded policy;
- `5xx` / permanent → fail the message without indefinite retry;
- connection/TLS failure → session recovery;
- authentication failure → stop/reported configuration or credential failure.

### Slice 3 — 100 messages + temporary failures

Introduce explicit retry/backoff/jitter and attempt limits.

### Slice 4 — 1000 messages / multiple domains

Introduce scheduler routing and independent provider/domain rate limits.

Example target:

```text
                 ┌────────────── domain X ──────────────┐
                 │                                       │
Queue → Scheduler ─→ Worker A → Session A                │
                 └→ Worker B → Session B                │
                                                         │
                 ┌────────────── domain Y ──────────────┘
                 └→ Worker C → Session C
```

### Slice 5 — restart safety

Introduce durable queue, leases, recovery and idempotent state transitions.

## Decision rules

1. Prefer mechanisms demonstrated by mature projects over speculative abstractions.
2. Keep external projects as references; do not introduce unnecessary source dependencies.
3. Every adopted mechanism must have a concrete requirement/test demonstrating why it belongs.
4. Do not create the full worker/session pool before the single-session vertical slice is validated.
5. Do not confuse concurrency limits, rate limits and retry backoff.
6. Do not treat SMTP `250` acceptance as final delivery.
7. Never implement stealth, identity rotation or provider-control bypassing.

## Current status

- MailKit is the primary SMTP protocol/session reference.
- MailSenderEngine currently implements the first single-message slice.
- Persistent multi-message worker/session routing is planned for Slice 2.
- Reference research should be expanded before finalizing worker, retry, queue and rate-limiting designs.
