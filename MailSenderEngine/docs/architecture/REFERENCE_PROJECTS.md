# MailSenderEngine — Reference Projects & Architecture Decisions

## Purpose

This document records external open-source projects inspected as architectural references for MailSenderEngine.

References are used to understand proven mechanisms, not to copy implementations blindly.

The target is a legitimate, policy-compliant SMTP delivery engine.

## Reference projects inspected

| Project | Area | What we take | What we do NOT take |
|---|---|---|---|
| MailKit | SMTP protocol/session | persistent `SmtpClient`, async/cancellation, SMTP response handling, reconnect semantics | custom SMTP implementation |
| Catapulte | outbound delivery platform | persistent SMTP connections, sender/domain routing, quotas, queue states, idempotency, lifecycle events, bounded worker concurrency | Rust-specific implementation, unnecessary NATS/Postgres dependency |
| SmtpGateway | .NET durable gateway | durable queue + spool, at-least-once delivery, per-recipient status, retry/backoff, operator visibility | inbound SMTP gateway complexity unless later required |
| maild | outbound control plane | queue → worker → retry/backoff, safety/policy boundary, delivery state model | cold-email/mass-mailing behavior |
| Hedwig | durable queue design | explicit ready/deferred/in-flight state, append/storage separation, crash/restart thinking | premature custom log storage |
| NLog.MailKit | throttling/observability integration | structured logging and the principle that throttling belongs outside the SMTP transport | NLog as a mandatory dependency |

## Findings adopted into MailSenderEngine

### 1. Persistent SMTP sessions are mandatory for the next slice

MailKit explicitly supports reusing one SMTP connection for multiple messages and notes that connection/authentication setup is expensive. The next implementation therefore keeps one connected session per worker and sends a batch of messages through it before disconnecting or recovering the session.

Reference: `jstedfast/MailKit`.

### 2. Sender/provider routing belongs above the SMTP session

Catapulte demonstrates a useful separation: configured SMTP senders can have routing rules and quotas while the sender connection itself remains an implementation detail. We adopt the same principle:

```text
Queue
  ↓
Scheduler
  ↓
Provider/domain route
  ↓
Worker
  ↓
Persistent SMTP session
  ↓
MailKit
```

The queue must not know how SMTP connections work, and an SMTP session must not select arbitrary jobs.

### 3. Connection pooling must be bounded and justified by concurrency

Catapulte currently keeps a reusable connection per configured sender and explicitly avoids a larger pool until concurrent in-flight sending requires it. MailKit also recommends reusing a small number of connections for bulk sending.

For MailSenderEngine:

- Slice 2: exactly one persistent session per worker.
- Later: a bounded number of sessions per provider/domain when measured concurrency requires it.
- Never create a connection per message in the bulk path.
- Never concurrently call `SendAsync` on the same `SmtpClient` instance.

### 4. Retry state must be explicit

The inspected projects consistently separate temporary delivery failure from permanent failure. MailSenderEngine will use explicit states rather than retrying arbitrary exceptions:

```text
Queued
  ↓
Sending
  ├── Accepted
  ├── RetryWait → Queued
  └── Failed / Bounced
```

Minimum classifier for Slice 2:

- SMTP `4xx` → temporary; retry under bounded policy.
- SMTP `5xx` → permanent; do not retry indefinitely.
- connection/TLS failure → session recovery + retry according to attempt policy.
- authentication/configuration failure → stop the affected worker/provider and report configuration failure.
- cancellation → propagate; do not convert into a delivery retry automatically.

### 5. Backoff and rate limiting are different mechanisms

The research confirms that retry backoff belongs to failure recovery, while quotas/rate limits control normal sending throughput. They must remain separate components.

Target separation:

```text
RetryPolicy      → when a failed message may be attempted again
RateLimiter      → whether a normal send may start now
WorkerConcurrency→ how many sends/sessions may be active
```

Do not use retry delays as a substitute for rate limiting.

### 6. Domain/provider limits are scheduling dimensions

Catapulte exposes sender quotas and optional recipient-domain routing. Other mail systems also demonstrate per-domain limits. For MailSenderEngine this becomes a later scheduling layer:

```text
Global limit
   + Provider limit
   + Recipient-domain limit
   + Worker/session concurrency
```

A domain limit must not be implemented by opening more SMTP connections or rotating identities. Provider controls are respected, not bypassed.

### 7. Durable delivery must be at-least-once, not falsely exactly-once

SmtpGateway explicitly documents at-least-once delivery and the possibility of duplicates after crash/retry. Catapulte also uses idempotency keys at submission level.

For our durable queue design:

- every message gets a stable ID;
- attempts are persisted;
- retry scheduling is persisted;
- worker claims/leases are recoverable after restart;
- terminal state is persisted;
- exactly-once external SMTP delivery is not promised.

Idempotency protects our own state transitions and submissions; it cannot make SMTP delivery itself exactly-once.

### 8. Queue state must be separated from transport state

Hedwig's ready/deferred/in-flight model is useful conceptually. We should preserve this separation without prematurely building a custom append-only queue.

The queue owns:

- message identity;
- payload reference;
- state;
- attempt count;
- next-attempt timestamp;
- lease/worker ownership;
- terminal result.

The SMTP session owns only transport state:

- connected/authenticated state;
- capabilities;
- connection health;
- reconnect/disconnect.

### 9. Observability is part of the delivery state machine

Catapulte exposes lifecycle events such as queued, sending, retrying, succeeded and failed. SmtpGateway provides queue inspection and provider health tooling.

MailSenderEngine should therefore log structured events around:

- queue transition;
- worker claim/release;
- SMTP connect/auth/disconnect;
- send attempt;
- SMTP response class;
- retry scheduling;
- terminal failure;
- cancellation.

Secrets and message bodies must never be logged by default.

## Reference research deliberately rejected

Some projects advertise SMTP rotation, identity rotation or techniques intended to reduce provider blocking/blacklisting. Those mechanisms are **not** architectural inputs for MailSenderEngine. We will not implement identity rotation, stealth behavior, provider-control bypasses or artificial evasion mechanisms.

Likewise, we will not add Redis/NATS/Postgres/custom spool infrastructure until the actual workload requires it and a concrete vertical slice demonstrates the need.

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
8. Prefer a small, testable state machine over infrastructure added only for theoretical scale.
9. Introduce durable storage only when restart-safety requirements justify it.

## Current status

- MailKit is the primary SMTP protocol/session reference.
- Catapulte is the primary reference for sender routing, quotas, persistent sender connections and lifecycle modeling.
- SmtpGateway is the primary reference for .NET durable delivery semantics and at-least-once recovery.
- Hedwig is a conceptual reference for separating ready/deferred/in-flight queue state; its custom log storage is not being copied.
- MailSenderEngine currently implements the first single-message slice.
- Next implementation target: Slice 2 — one persistent SMTP session sending multiple messages with minimal 4xx/5xx classification and recovery.
