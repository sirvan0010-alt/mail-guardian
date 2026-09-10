# MailSenderEngine

C#/.NET application for reliable, policy-compliant email submission and delivery workflow.

## Scope

MailSenderEngine is deliberately separate from the Python MailGuardian forensic analyzer.

It is responsible for:

- durable email queue;
- explicit message lifecycle/state;
- bounded asynchronous workers;
- persistent MailKit SMTP sessions;
- rate limiting and provider throttling response;
- bounded retry/backoff;
- SMTP error classification;
- structured observability;
- cancellation and graceful shutdown;
- optional DSN/bounce processing;
- pluggable message generation through `IMailPayloadPlugin`.

It must not contain EML forensic-analysis business logic from MailGuardian.

## Security

SMTP configuration must be supplied through environment/configuration/secret providers. Never commit credentials.

Example environment variables:

```text
MAILSENDER_SMTP_HOST=smtp.example.com
MAILSENDER_SMTP_PORT=587
MAILSENDER_SMTP_USER=example
MAILSENDER_SMTP_PASSWORD=<secret>
```

## Development order

1. configuration and domain model;
2. explicit message state machine;
3. payload plugin boundary;
4. persistent SMTP session manager;
5. durable queue;
6. bounded workers;
7. rate limiting;
8. retry/backoff;
9. observability;
10. DSN/bounce processing;
11. integration/load tests.

Do not optimize throughput before correctness, persistence and failure recovery are established.
