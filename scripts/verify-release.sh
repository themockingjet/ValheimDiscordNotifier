#!/usr/bin/env bash
set -Eeuo pipefail

SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd -- "$SCRIPT_DIR/.." && pwd)"
RELEASE_DIR="${RELEASE_DIR:-$PROJECT_ROOT/release}"
RELEASE_ARCHIVE="${RELEASE_ARCHIVE:-}"

cd "$PROJECT_ROOT"
if [[ "$RELEASE_DIR" != /* ]]; then
	RELEASE_DIR="$PROJECT_ROOT/$RELEASE_DIR"
fi
if [[ -n "$RELEASE_ARCHIVE" && "$RELEASE_ARCHIVE" != /* ]]; then
	RELEASE_ARCHIVE="$PROJECT_ROOT/$RELEASE_ARCHIVE"
fi
make validate-release

if [[ -z "$RELEASE_ARCHIVE" ]]; then
	RELEASE_ARCHIVE="$(find "$RELEASE_DIR" -maxdepth 1 -type f -name '*.zip' \
		-print -quit)"
fi

[[ -n "$RELEASE_ARCHIVE" && -f "$RELEASE_ARCHIVE" ]] || {
	printf 'Release ZIP was not found in %s.\n' "$RELEASE_DIR" >&2
	printf 'Run scripts/package.sh before verifying the release.\n' >&2
	exit 1
}

unzip -tq "$RELEASE_ARCHIVE"
mapfile -t archive_files < <(unzip -Z1 "$RELEASE_ARCHIVE")
expected_files=(
	CHANGELOG.md
	README.md
	ValheimDiscordNotifier.dll
	icon.png
	manifest.json
)

if [[ "${#archive_files[@]}" -ne "${#expected_files[@]}" ]]; then
	printf 'Release ZIP has unexpected file count: expected %s, found %s.\n' \
		"${#expected_files[@]}" "${#archive_files[@]}" >&2
	printf 'Files found:\n%s\n' "$(printf '%s\n' "${archive_files[@]}")" >&2
	exit 1
fi

for expected_file in "${expected_files[@]}"; do
	if ! printf '%s\n' "${archive_files[@]}" | grep -Fxq "$expected_file"; then
		printf 'Release ZIP is missing or renaming required file: %s\n' "$expected_file" >&2
		exit 1
	fi
done

if printf '%s\n' "${archive_files[@]}" | grep -Eq \
	'(^|/)(BepInEx|ServerSync|config|managed|plugins)(/|$)|(^|/)(assembly_.*|UnityEngine.*|BepInEx|0Harmony).*\.dll$'; then
	printf 'Release ZIP contains forbidden game, loader, ServerSync, plugin-path, or config files.\n' >&2
	exit 1
fi

if [[ -f "$RELEASE_ARCHIVE.sha256" ]]; then
	(
		cd "$(dirname "$RELEASE_ARCHIVE")"
		sha256sum -c "$(basename "$RELEASE_ARCHIVE").sha256"
	)
fi

printf 'Release package is valid: %s\n' "$RELEASE_ARCHIVE"
