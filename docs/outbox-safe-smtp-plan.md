# Outbox-safe SMTP implementation plan and progress

Branch: `feature/outbox-safe-smtp`. Approved scope: additive stable MessageId, TLS mode, deadline,
transient-only safe failure policy; accepted submission survives cleanup failure. No durable queue.

- [x] Baseline full Release suite: 46 passed; regression file excluded for baseline (duplicate
  generated-source warnings from overriding exclusions only).
- [x] RED: seven behavior regressions failed (identity, TLS, disconnect/disposal acceptance,
  permanent rejection, uncertain response, timeout); original 46 passed.
- [x] GREEN: implementation with additive contracts and legacy defaults; 56 passed with no warnings.
- [x] Safe caller cancellation RED then GREEN; authentication and no-secret log checks.
- [x] Strengthened identity across actual submission attempts and deadline during send/retry delay.
- [x] Caller deadline cancels asynchronous options retrieval before client creation; documented
  whole-operation caller budget and sanitized provider/MIME boundary.
- [x] Update public XML and package/operator documentation.
- [x] Exact full Release workflow: restore succeeded; build succeeded with zero warnings/errors;
  `dotnet test --solution SyntaxCircus.Email.slnx --no-build --configuration Release`: 59 passed,
  zero failed/skipped (6.652 seconds). `git diff --check` succeeded.
- [ ] Independent review (parent orchestrator).
- [x] Commit on owner branch. No push, publication or merge.

Real relay/lab delivery and consuming application's durable-state/crash tests remain external
acceptance requirements. Unit transport tests use the package's existing MailKit client seam.
