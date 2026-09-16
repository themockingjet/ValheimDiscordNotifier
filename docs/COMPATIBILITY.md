# Valheim Discord Notifier Compatibility

## Supported environment

- **Valheim:** dedicated server assemblies from the shared local reference
  environment.
- **BepInEx:** 5.x, including `denikson-BepInExPack_Valheim-5.4.2202` or a
  compatible release.
- **Runtime:** .NET Framework 4.8 (`net48`).
- **Discord:** an incoming webhook endpoint reachable over HTTPS.

No ServerSync, ConfigSync, client version handshake, or synchronized
configuration is used.

## Server-only behavior

This is a server-only plugin. The dedicated server administrator installs
`ValheimDiscordNotifier.dll` in `BepInEx/plugins/`; clients do not install it
and do not need a matching client mod. `Application.isBatchMode` prevents the
plugin from activating outside the dedicated server process.

The plugin does not patch chat, expose commands, own networked objects, control
the server process, or add a restart/watchdog wrapper.

## Discord dependency

Notifications require a valid Discord incoming webhook URL configured in the
server's persistent
`BepInEx/config/io.hexium.valheim.discordnotifier.cfg` file. URLs must be
absolute HTTPS URLs without user information. Webhook URLs are secrets: keep
the configuration file server-local and never commit or distribute it.

## Validation matrix

| Area | Validation |
| --- | --- |
| Environment | `make preflight` confirms the shared paths, required Valheim/BepInEx references, .NET SDK, solution, project, and release inputs. |
| Build | `make build` compiles the SDK-style `net48` project with manual assembly metadata and shared references. |
| Client safety | Loading outside batch mode logs a warning and does not bind configuration or install lifecycle hooks. |
| Readiness | A dedicated `ZNet.OnGenerationFinished` callback sends at most one ready notification. |
| Shutdown | A dedicated `ZNet.Shutdown` prefix sends at most one stopping notification; `OnApplicationQuit` is a fallback. |
| URL rules | Empty, malformed, HTTP, and user-info URLs are rejected; valid HTTPS URLs proceed to request creation. |
| Request limits | Timeout values are bounded to 1–30 seconds and message content is trimmed and capped at 2,000 characters. |
| Failure handling | HTTP errors, timeouts, I/O failures, and unsupported-runtime failures are logged without interrupting Valheim lifecycle processing. |
| Package | `make verify-release` requires exactly the manifest, README, changelog, icon, and `ValheimDiscordNotifier.dll` at the ZIP root, with no game, loader, ServerSync, config, or secret files. |
