# Valheim Discord Notifier Instructions

## Purpose and scope

This repository contains `ValheimDiscordNotifier`, a server-only BepInEx 5
plugin for Valheim dedicated servers. It sends best-effort Discord webhook
notifications for server readiness and normal shutdown only.

Do not add chat relay, remote-control commands, restart wrappers, watchdogs,
client-side behavior, or gameplay/networked-state mutations without an
explicit product decision.

## Compatibility and identity

- Plugin GUID: `io.hexium.valheim.discordnotifier`
- Plugin assembly: `ValheimDiscordNotifier`
- Root namespace: `ValheimDiscordNotifier`
- Thunderstore package: `ValheimDiscordNotifier`
- Version: `0.1.2`
- Runtime target: .NET Framework 4.8.
- Loader: BepInEx 5.
- Clients do not install this plugin.
- Do not introduce ServerSync, ConfigSync, synchronized configuration, or a
  client version handshake.

Preserve the existing BepInEx configuration section/key names, blank webhook
default, and manual `Properties/AssemblyInfo.cs` metadata, including Hexium
attribution. Do not enable SDK-generated assembly metadata.

## Runtime and security rules

- Keep the `Application.isBatchMode` guard.
- Keep readiness on `ZNet.OnGenerationFinished` and normal shutdown on
  `ZNet.Shutdown`, with the application-quit fallback and per-event
  deduplication.
- Keep HTTPS-only endpoint validation, malformed/user-info rejection, timeout
  bounds, content limits, and best-effort failure logging.
- Never put a real webhook URL or usable secret in source, documentation,
  package assets, sample configuration, or Git history.
- Keep delivery failures from interrupting Valheim lifecycle processing.

## Build and release

Source the shared environment before using the standard workflow:

```sh
source "$HOME/.config/valheim-dev/env.sh"
make preflight
make build
make package
make verify-release
```

Use `VALHEIM_MANAGED_PATH` and `BEPINEX_PATH` for external references. Keep
Valheim and BepInEx assemblies outside the repository. Generated output belongs
only in ignored `release/`, `bin/`, and `obj/` directories.

The release ZIP must contain exactly `manifest.json`, `README.md`,
`CHANGELOG.md`, `icon.png`, and `ValheimDiscordNotifier.dll` at its root.
Never package configuration files, game/loader DLLs, ServerSync, or secrets.

## Change discipline

Read `docs/ARCHITECTURE.md` and `docs/COMPATIBILITY.md` before changing
lifecycle or configuration code. Make precise changes, preserve public
identifiers, and update the documentation and validation matrix when
compatibility or runtime behavior changes. Do not modify other repositories.
