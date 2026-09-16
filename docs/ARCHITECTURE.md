# Valheim Discord Notifier Architecture

## Purpose

Valheim Discord Notifier is a server-only BepInEx plugin. It emits one
best-effort Discord webhook notification when a dedicated server becomes ready
and one when normal shutdown begins. It does not change gameplay, relay chat,
accept remote commands, or manage process restarts.

## Lifecycle hooks and flow

1. BepInEx loads `DiscordNotifierPlugin`.
2. `Awake` exits immediately with a warning unless Unity reports
   `Application.isBatchMode`; the plugin therefore remains inactive in a
   client or non-dedicated process.
3. In a dedicated server, the plugin binds its existing server-local BepInEx
   configuration entries and installs Harmony hooks for
   `ZNet.OnGenerationFinished` and `ZNet.Shutdown`.
4. The `OnGenerationFinished` postfix requires a server and dedicated-server
   `ZNet` instance, deduplicates the event, validates the configured request,
   and queues the ready notification on the thread pool.
5. The `Shutdown` prefix requires a dedicated server, deduplicates the event,
   validates the configured request, and sends the stopping notification
   synchronously so normal shutdown has an opportunity to finish delivery.
6. `OnApplicationQuit` provides the shutdown fallback when the normal
   `ZNet.Shutdown` hook is not reached. The same atomic deduplication prevents
   a second stopping notification.

## Components and boundaries

| Component | Responsibility | Boundary |
| --- | --- | --- |
| `DiscordNotifierPlugin` | Configuration, lifecycle state, logging, and request dispatch | BepInEx and Unity |
| `ZNetOnGenerationFinishedPatch` | Detects dedicated-server readiness | Valheim `ZNet.OnGenerationFinished` |
| `ZNetShutdownPatch` | Detects the beginning of normal shutdown | Valheim `ZNet.Shutdown` |
| `WebhookRequest` | Carries an already validated endpoint, payload, timeout, and event name | `HttpWebRequest` |

The plugin does not mutate networked Valheim state. All state is process-local:
the configured values come from the server's persistent BepInEx config and the
two notification flags are in-memory atomics.

## Configuration validation

- `Discord.WebhookUrl` defaults to blank and is read only from the persistent
  server-local BepInEx configuration.
- Empty values are skipped without a request.
- The endpoint must be an absolute HTTPS URI with a non-empty host.
- Malformed URLs, non-HTTPS schemes, and URLs containing user information are
  rejected before any network operation.
- `Discord.TimeoutSeconds` is accepted only from 1 through 30 seconds;
  out-of-range values log a warning and use the five-second safe default.
- Messages are trimmed, empty messages are skipped, and content is capped at
  Discord's 2,000-character limit before JSON escaping.

## Failure behavior and deduplication

Delivery is deliberately best effort. Request creation, timeout, I/O,
unsupported-runtime, HTTP, and network failures are logged as warnings and do
not escape into Valheim's startup or shutdown lifecycle. A non-2xx response is
also logged and does not interrupt the game.

Readiness and stopping each use an `Interlocked.Exchange` guard. Repeated
Harmony callbacks or the application-quit fallback therefore produce at most
one notification per event per process. Forced kills, crashes, and power loss
are outside the guarantees of a process-local plugin.
