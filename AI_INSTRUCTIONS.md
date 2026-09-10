# MailGuardian — AI Engineering Instructions

## 1. Purpose of this file

This document is the operating contract for any AI agent, coding assistant, reviewer, or developer working on this repository.

Before changing code, read this file and inspect the actual repository contents. Do not assume that code snippets from conversations or previous audits represent the current repository state.

The repository is **MailGuardian / MailSenderEngine**, a C#/.NET project intended to evolve from an SMTP load-test/prototype into a robust, modern, automated email-sending engine.

Primary goals:

- reliable high-volume email submission;
- persistent SMTP sessions where supported;
- bounded concurrency;
- adaptive rate limiting and backoff;
- durable email queueing;
- retry handling;
- classification of temporary vs permanent SMTP failures;
- bounce/failure processing;
- delivery/open/click tracking where legally and technically appropriate;
- structured observability and diagnostics;
- cancellation and graceful shutdown;
- secure configuration;
- clean separation between transport, scheduling, message generation and business logic;
- maintainable production-quality C# code.

This is **not merely a script that sends 50 test messages**. Load-testing code may exist as a diagnostic/prototype layer, but it must not be confused with the target production architecture.

---

## 2. Technology direction

Preferred implementation language: **C#**.

Preferred runtime direction: modern **.NET** (use the version actually defined by the project files; do not upgrade blindly).

SMTP/MIME libraries:

- MailKit
- MimeKit

Use asynchronous APIs throughout I/O paths.

Required engineering properties:

- thread-safe shared state;
- `CancellationToken` propagation;
- bounded concurrency;
- no blocking waits such as `.Result` or `.Wait()` in asynchronous execution paths;
- no hard-coded credentials;
- deterministic state transitions;
- testable services instead of putting all logic in `Program.cs`.

---

## 3. Security rules

NEVER commit:

- SMTP passwords;
- API keys;
- OAuth secrets;
- connection strings containing credentials;
- private keys;
- production recipient databases;
- customer personal data;
- tracking identifiers tied to real users unless intentionally supplied as test fixtures.

Configuration must come from environment variables, configuration providers, secret stores, or explicitly supplied secure configuration.

Development examples must use placeholders.

If existing source contains credentials or secrets, treat that as a security defect. Do not propagate the secret into new files, tests, documentation or commits.

---

## 4. Current repository state

The current `main` branch may contain an archive (`MailGuardian.zip`) rather than a normal expanded source tree. If the source is packaged in an archive:

1. inspect the archive before making architectural decisions;
2. extract it in the working environment;
3. identify the actual `.sln`, `.csproj`, source files and tests;
4. preserve the original archive unless there is a deliberate reason to replace it;
5. prefer converting the repository into a normal source-controlled project structure when appropriate, with a separate migration commit.

Never claim that a feature exists merely because an earlier conversation or audit says it exists. Verify the current source.

---

## 5. Target architecture

The desired architecture should evolve toward these logical components:

```text
                    MailGuardian
                         |
        +----------------+----------------+
        |                                 |
   Application                         Observability
        |                                 |
        v                                 v
   Scheduler / Orchestrator ------ Structured Logging
        |
        v
   Durable Email Queue
        |
        v
   Rate / Retry Controller
        |
        v
   Bounded Worker Pool
        |
        v
   SMTP Session Manager / Pool
        |
        v
      MailKit
        |
        v
    SMTP Provider
```

Message lifecycle should eventually be explicit and persistent, for example:

```text
QUEUED
  -> SENDING
  -> SENT / ACCEPTED
  -> DELIVERED
  -> OPENED
  -> CLICKED

or

SENDING
  -> RETRY_WAIT
  -> QUEUED

or

SENDING
  -> FAILED
  -> BOUNCED
```

Do not introduce states casually. Document the state machine and make transitions idempotent.

---

## 6. SMTP connection strategy

Persistent SMTP sessions are preferred over connecting/authenticating/disconnecting for every message when the SMTP provider permits it.

Preferred pattern:

```text
worker
  -> ConnectAsync
  -> AuthenticateAsync
  -> Send message 1
  -> Send message 2
  -> Send message 3
  -> ...
  -> DisconnectAsync
```

Do not create one `SmtpClient` per message unless a concrete provider requirement or reliability strategy requires it.

Important: `MailKit.Net.Smtp.SmtpClient` is not a general-purpose thread-safe shared client. Do not concurrently call `SendAsync` on the same SMTP client. Each session/connection must have exclusive ownership by one worker at a time.

Connection reuse is an optimization, not a reason to ignore server limits. Respect SMTP provider policies, connection limits, idle timeouts and throttling responses.

---

## 7. Concurrency and rate limiting

Do not confuse these concepts:

### Concurrency
How many SMTP operations/sessions may execute concurrently.

Use mechanisms such as `SemaphoreSlim` or a bounded worker pool.

### Rate limiting
How many messages/SMTP operations may be submitted during a time interval.

### Backoff
How the sender reacts to temporary failures or throttling.

The initial prototype uses a shared delay controlled with `Volatile`/`Interlocked`. That is useful as an experiment but is not sufficient as the final production design.

The target design should support, where justified:

- global rate limits;
- per-provider or per-domain limits;
- configurable maximum concurrency;
- minimum/maximum delay;
- exponential or decorrelated backoff;
- jitter;
- server response driven throttling;
- reset/recovery after successful sends.

Do not blindly increase throughput after success. Rate adaptation must have safe upper bounds.

---

## 8. SMTP error classification

Do not treat every SMTP exception as a retryable failure.

At minimum distinguish:

- successful acceptance;
- temporary SMTP failures (`4xx`);
- permanent SMTP failures (`5xx`);
- connection/TLS failures;
- authentication failures;
- cancellation;
- local serialization/message-generation failures.

The exact retry policy must be provider-aware and configurable.

Typical temporary failures may be retried with bounded attempts and backoff. Permanent recipient errors should normally be marked failed/bounced rather than retried indefinitely.

Never implement infinite retries.

Every retry must preserve correlation/message identity and increment an attempt counter.

---

## 9. Durable queue

The production engine must not depend on an in-memory array such as:

```csharp
var tasks = new Task[totalMessages];
```

for durable campaign execution.

A real queue should provide:

- persistence;
- atomic claim/lease semantics;
- retry scheduling;
- attempt count;
- status;
- timestamps;
- error information;
- idempotency/correlation identifiers;
- graceful recovery after process restart.

The storage technology must be chosen after inspecting the existing project and requirements. Do not add a database solely because it is fashionable.

---

## 10. Tracking and bounces

SMTP `250` acceptance is not equivalent to final delivery.

The architecture should distinguish:

```text
SMTP ACCEPTED
        |
        +--> later DSN/bounce processing
        |
        +--> delivery state when reliable evidence exists
```

Bounce/DSN processing should be a separate concern from the SMTP sending loop.

Open tracking normally requires an HTML tracking pixel and click tracking normally requires redirect links. These mechanisms have privacy, legal, client-side blocking and consent implications. Do not silently add tracking to customer communications.

Tracking must be configurable and disabled by default unless the customer explicitly requires it.

---

## 11. Message identity and observability

Every logical email should have stable identifiers, for example:

- `MessageId` / provider-safe message identifier;
- `CampaignId`;
- `RecipientId` where applicable;
- `CorrelationId`;
- attempt number.

Do not use random header rotation as a substitute for identity or deliverability engineering.

Prefer structured logging over `Console.WriteLine` in production services.

Logs should make it possible to answer:

- what was sent;
- to whom, subject to privacy policy;
- when;
- through which provider/session;
- which attempt;
- SMTP response/status;
- whether it will retry;
- when the next retry is scheduled;
- why a message became permanently failed.

Never log SMTP passwords or authorization tokens.

---

## 12. Plugin/message-generation architecture

Keep network transport separate from message generation/business logic.

The target architecture may use a plugin abstraction such as:

```csharp
public interface IMailPayloadPlugin
{
    Task<MimeMessage> BuildAsync(
        MailPayloadContext context,
        CancellationToken cancellationToken);
}
```

The exact API must follow the existing repository if such an abstraction already exists. Do not introduce a duplicate abstraction without first checking the source.

SMTP code should not know campaign-specific business rules.

---

## 13. Cancellation and shutdown

All asynchronous operations must accept and propagate `CancellationToken` where supported.

On shutdown:

1. stop accepting new work;
2. allow safe in-flight operations to finish when possible;
3. persist/release queue leases;
4. disconnect SMTP sessions cleanly where possible;
5. exit without corrupting message state.

Do not swallow `OperationCanceledException` as a generic error.

---

## 14. Code quality rules

Before modifying a class:

1. read the complete class;
2. identify its callers;
3. identify shared state;
4. identify threading assumptions;
5. identify configuration dependencies;
6. inspect tests if present.

Avoid large rewrites unless the existing design is demonstrably unsuitable.

Prefer small, reviewable commits.

Do not mix unrelated refactoring with feature work.

Do not rename public APIs without checking all consumers.

Do not add abstractions merely to increase the number of classes.

Do not hide exceptions without recording actionable diagnostic information.

Do not use synchronous I/O inside async workflows.

---

## 15. Testing requirements

New functionality should include appropriate tests.

At minimum, test where applicable:

- SMTP success;
- temporary `4xx` failure;
- permanent `5xx` failure;
- retry limit;
- backoff calculation;
- cancellation;
- duplicate/idempotent processing;
- queue recovery;
- concurrent workers;
- rate limiter boundaries;
- configuration validation.

Network integration tests must not require production credentials. Use mocks/fakes or a dedicated local test SMTP server.

---

## 16. AI workflow — mandatory

When an AI agent enters this repository, it must follow this sequence:

### Phase A — Inspect

- inspect the complete repository tree;
- inspect project files and dependencies;
- inspect the current branch and recent commits if available;
- inspect existing documentation;
- inspect all relevant source files;
- determine whether the code is prototype, test harness, or production service;
- identify duplicated/competing implementations.

### Phase B — Report

Before a large change, state:

- current architecture;
- what already works;
- what is incomplete;
- risks;
- proposed minimal change;
- files that will be changed.

### Phase C — Implement

Implement the smallest coherent change that moves the architecture toward the target state.

### Phase D — Validate

- build;
- run tests;
- inspect compiler warnings/errors;
- verify cancellation and error paths;
- verify no secrets were introduced;
- inspect the resulting diff.

### Phase E — Document

Update documentation when architecture or operational behavior changes.

### Phase F — Commit

Use a descriptive commit message explaining the actual change.

Do not claim that a feature is complete without validation.

---

## 17. Important anti-patterns

Do NOT:

- hard-code SMTP credentials;
- create a new SMTP connection for every message without justification;
- use unlimited parallel tasks for mass sending;
- retry permanent failures forever;
- use one global delay as the entire rate-limiting strategy;
- treat SMTP `250` as guaranteed final delivery;
- put all production logic into `Program.cs`;
- rotate sender identities/headers merely to evade provider controls;
- bypass spam/abuse controls;
- implement stealth or evasion mechanisms;
- log secrets;
- make claims based only on previous AI conversation context;
- delete working functionality without understanding its purpose.

This project should behave as a legitimate, policy-compliant mail delivery system, not as a spam-evasion or anti-abuse bypass tool.

---

## 18. Definition of done

A feature is not considered complete merely because it compiles.

For production-oriented changes, definition of done is:

- code is implemented in the correct architectural layer;
- asynchronous and cancellation-safe;
- thread-safe where shared state exists;
- configuration is secure;
- errors are classified correctly;
- retry behavior is bounded and deterministic;
- state changes are observable;
- tests cover important failure paths;
- documentation is updated where needed;
- build/tests pass;
- diff has been reviewed for unintended changes.

---

## 19. Current priority roadmap

Unless the repository inspection reveals a more urgent defect, work should generally progress in this order:

1. **Repository/source normalization and baseline build**
2. **Architecture separation from prototype `Program.cs`**
3. **SMTP session manager with safe persistent sessions**
4. **Durable queue and explicit message state machine**
5. **Bounded worker pool + rate limiter**
6. **Retry/backoff and SMTP error classification**
7. **Structured logging/metrics**
8. **Bounce/DSN processing**
9. **Delivery/tracking subsystem, only when explicitly required**
10. **Configuration, deployment and operational hardening**
11. **Integration/load testing**

Do not jump to tracking or UI before the sending core is reliable.

---

## 20. Final instruction to AI agents

You are working on **MailGuardian**, a C#/.NET email automation and delivery engine.

Treat the repository as the source of truth.

Inspect first. Understand second. Change third. Validate fourth.

Preserve working behavior unless there is a clear reason to change it.

When an architectural decision is uncertain, document the trade-off rather than silently choosing a complex solution.

The objective is a maintainable, secure, observable and reliable production mail engine using MailKit/MimeKit — not merely a faster SMTP stress test.
