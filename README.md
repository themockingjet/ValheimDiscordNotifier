# Valheim Discord Notifier

Valheim Discord Notifier is a server-only BepInEx plugin that sends Discord
webhook notifications when a dedicated Valheim server becomes ready and when
normal shutdown begins. It does not relay chat or provide remote control.

## Development setup

Load the shared Valheim reference environment and run the standard validation
and release workflow from the repository root. Make targets automatically load
`$HOME/.config/valheim-dev/env.sh` when it exists; source that file manually
only for direct shell commands outside Make:

```sh
make preflight
make build
make package
make verify-release
```

The package is written to
`release/ValheimDiscordNotifier-0.1.1.zip`. Its root contains only the
Thunderstore metadata, icon, changelog, and `ValheimDiscordNotifier.dll`.

## Deploy to a test server

Build, verify, and install the local package into the active BepInEx release:

```bash
make deploy-test-server TEST_SERVER=local
```

For an SSH test server, use `TEST_SERVER=user@host`. The helper installs the
root-level DLLs into a separate `local-*` directory under
`/opt/valheim/modpack/current/BepInEx/plugins`, preserves the server and
maintenance-timer state without starting or stopping either one, and leaves
the managed Hexium manifest unchanged.
Use `TEST_SERVER_SSH_OPTIONS="-p 2222"` for a non-default SSH port. Remove
the temporary install with:

```bash
make remove-test-server TEST_SERVER=local
```

The installer never calls `systemctl`. To batch-install several mods and
restart once, stop the server and timer yourself, run this command from each
mod repository, then start them once:

```bash
sudo systemctl stop valheim-restart.timer
sudo systemctl stop valheim.service
make deploy-test-server TEST_SERVER=local
# Repeat from each mod repository.
sudo systemctl start valheim.service
sudo systemctl start valheim-restart.timer
```

Set `TEST_SERVER_SUDO=` when running directly as root. Use
`TEST_PLUGIN_DIR=local-OtherName` to keep multiple local builds separate.

## Project layout

- `src/valheim-discord-notifier/`: plugin source, manual assembly metadata,
  and the SDK-style project.
- `Thunderstore/`: package manifest, user documentation, changelog, and icon.
- `docs/`: architecture, compatibility, and icon guidance.
- `scripts/`: reference setup, build, package, and release verification.
- `release/`: generated local build and package output; it is ignored by Git.

The build uses the shared `VALHEIM_MANAGED_PATH` and `BEPINEX_PATH`
environment variables. Valheim and BepInEx assemblies remain outside this
repository.

## Scope and security

Webhook URLs remain blank by default and are configured only in the
server-local BepInEx configuration file. Never commit or distribute a real
webhook URL. The plugin validates HTTPS endpoints, rejects malformed and
user-info URLs, bounds request timeouts, limits Discord message content, and
keeps delivery best effort so network failures cannot interrupt Valheim.

Clients do not install this plugin, and the project deliberately has no
ServerSync dependency or client version handshake.
