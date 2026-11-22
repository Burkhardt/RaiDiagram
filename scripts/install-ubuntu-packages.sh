#!/usr/bin/env bash
# CI-only package provisioning using Ubuntu's signed archive, independently of
# incomplete runner source lists. See https://ubuntu.com/server/docs/package-management/
set -euo pipefail
. /etc/os-release
[[ "$ID" == ubuntu && -n "${VERSION_CODENAME:-}" ]] || { echo 'Ubuntu with VERSION_CODENAME is required' >&2; exit 2; }
[[ $# -gt 0 ]] || { echo 'Supply at least one package name' >&2; exit 2; }
arch=$(dpkg --print-architecture)
case "$arch" in
  amd64|i386) archive=https://archive.ubuntu.com/ubuntu; security=https://security.ubuntu.com/ubuntu ;;
  *) archive=https://ports.ubuntu.com/ubuntu-ports; security=$archive ;;
esac
sources=$(mktemp --suffix=.sources)
trap 'rm -f "$sources"' EXIT
chmod 644 "$sources"
cat > "$sources" <<EOF
Types: deb
URIs: $archive
Suites: $VERSION_CODENAME $VERSION_CODENAME-updates
Components: main universe
Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg

Types: deb
URIs: $security
Suites: $VERSION_CODENAME-security
Components: main universe
Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg
EOF
privilege=()
if [[ $(id -u) != 0 ]]; then privilege=(sudo); fi
# Use the same authenticated sources for both refresh and install. Preserve the
# runner's existing Ubuntu and third-party configuration without duplicates.
options=(-o "Dir::Etc::sourcelist=$sources" -o 'Dir::Etc::sourceparts=-')
"${privilege[@]}" apt-get "${options[@]}" update
"${privilege[@]}" apt-get "${options[@]}" install --yes --no-install-recommends "$@"
