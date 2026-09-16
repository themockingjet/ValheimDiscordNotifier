# Valheim Discord Notifier

A small, server-only BepInEx plugin that sends Discord webhook notifications
when a Valheim dedicated server becomes ready and when it begins a normal
shutdown. A systemd restart produces one stopping notification before the
server exits and one ready notification after the replacement server finishes
starting.

## Scope

This plugin intentionally does only lifecycle webhook notifications:

- no chat relay or in-game chat changes;
- no client installation or client configuration;
- no Discord bot token;
- no remote start, stop, or restart controls;
- no watchdog, restart timer, or auto-restart wrapper.

Webhook delivery is always best effort. A missing or invalid endpoint, a
Discord error, or a network timeout is logged by BepInEx and never throws out
of Valheim's startup or shutdown lifecycle path.

## Installation

1. Install BepInEx 5 for Valheim.
2. Copy `ValheimDiscordNotifier.dll` from the root of the release ZIP into the
   server's `BepInEx/plugins/` directory.
3. Start the dedicated server once. This creates the persistent server-local
   configuration file
   `BepInEx/config/io.hexium.valheim.discordnotifier.cfg`.
4. Stop the server normally, edit that file on the server to add its Discord
   webhook URL, then start it again.

The release ZIP contains no configuration file and no webhook URL. Do not put
a webhook URL in a public repository, mod pack, or shared default config.

## Configuration

The generated `BepInEx/config/io.hexium.valheim.discordnotifier.cfg` is the
persistent override location: BepInEx keeps it across plugin upgrades. Set the
webhook only there, with restrictive filesystem permissions appropriate for
the account that runs the server.

```ini
[Discord]
## HTTPS Discord webhook endpoint. It is intentionally blank by default; configure it only in this server's persistent BepInEx config.
# Setting type: String
# Default value:
WebhookUrl = https://discord.com/api/webhooks/WEBHOOK_ID/WEBHOOK_TOKEN

## Webhook timeout in seconds. Values outside 1 through 30 use the safe default of 5 seconds.
# Setting type: Int32
# Default value: 5
TimeoutSeconds = 5

[Messages]
ServerReady = Valheim server is ready.
ServerStopping = Valheim server is stopping.
```

`Discord.WebhookUrl` must be an absolute HTTPS URL. The plugin rejects empty,
HTTP, malformed, and user-info URLs without attempting a request. The timeout
is bounded to 1 through 30 seconds; an out-of-range value logs a warning and
uses 5 seconds. Messages are trimmed and limited to Discord's 2,000-character
content limit.

The example URL is illustrative, not a usable credential. Treat the complete
real webhook URL as a secret.

## Lifecycle behavior

The ready notification is issued after Valheim's `ZNet.OnGenerationFinished`
lifecycle callback for a dedicated server. The normal stop notification is
issued as `ZNet.Shutdown` begins, with an application-quit fallback. Duplicate
callbacks produce at most one notification per process for each event.

The shutdown request runs synchronously so the normal shutdown path has an
opportunity to send it before the process exits. It is capped by
`Discord.TimeoutSeconds`; all delivery failures are caught and logged, then
the normal shutdown continues. A forced kill, crash, power loss, or host
failure cannot reliably emit a notification and is outside this plugin's
scope.

## Using it with systemd and Valheim Lifecycle Announcer

This plugin does not install a systemd unit, `ExecStop` hook, timer, helper,
or restart policy. Keep using the existing
[Valheim Lifecycle Announcer](https://github.com/hexium/ValheimLifecycleAnnouncer)
systemd drop-in and local Unix-socket helper for in-game restart warnings.

With that architecture, systemd runs the announcer's `ExecStop=` warning
sequence before it asks Valheim to stop. Valheim then executes `ZNet.Shutdown`,
which sends this plugin's single Discord stopping notification. On a systemd
restart, the next process sends the ready notification after its world is
ready. Do not add a second `ExecStop=` or restart wrapper for this plugin.

Because the lifecycle announcer's warning sequence can take several minutes,
retain its existing `TimeoutStopSec` configuration. This notifier adds at
most its configured 1--30 second webhook timeout to the normal Valheim
shutdown path.

## Building and packaging

Build and verify the package from the repository root with the shared Valheim
reference environment:

```sh
source "$HOME/.config/valheim-dev/env.sh"
make preflight
make build
make package
make verify-release
```

The generated ZIP root contains exactly `manifest.json`, `README.md`,
`CHANGELOG.md`, `icon.png`, and `ValheimDiscordNotifier.dll`. It contains no
configuration file, game or loader assemblies, ServerSync assembly, or
webhook secret.

## Compatibility

- **Required:** BepInEx 5 for Valheim
  (`denikson-BepInExPack_Valheim` 5.4.2202 or compatible).
- **Server only:** clients do not need this mod.
- **Webhook:** an HTTPS endpoint compatible with Discord incoming webhooks.
