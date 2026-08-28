#!/usr/bin/env bash
# Per-boot Unity license activation. Runs on every environment start.
# Never fails the boot: without a license the VM is still usable (Node server, editing, greps),
# only the Unity EditMode suite needs activation.
set -uo pipefail

UNITY_VERSION="6000.5.6f1"
UNITY_BIN="/opt/unity/${UNITY_VERSION}/Editor/Unity"

if [ ! -x "${UNITY_BIN}" ]; then
  echo "[start] Unity not installed yet; skipping license activation."
  exit 0
fi

if [ -n "${UNITY_LICENSE:-}" ]; then
  # Personal license: UNITY_LICENSE holds the full text of a .ulf activation file
  # (the GameCI personal-license convention).
  echo "[start] Activating Unity Personal license from \$UNITY_LICENSE ..."
  LICENSE_FILE="$(mktemp --suffix=.ulf)"
  printf '%s' "${UNITY_LICENSE}" > "${LICENSE_FILE}"
  xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -quit \
    -manualLicenseFile "${LICENSE_FILE}" -logFile /tmp/unity_license.log || true
  rm -f "${LICENSE_FILE}"
  echo "[start] Done (see /tmp/unity_license.log)."
elif [ -n "${UNITY_SERIAL:-}" ] && [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
  # Plus / Pro license: activate a seat from the serial.
  echo "[start] Activating Unity Plus/Pro license from \$UNITY_SERIAL ..."
  xvfb-run -a "${UNITY_BIN}" -batchmode -nographics -quit \
    -serial "${UNITY_SERIAL}" -username "${UNITY_EMAIL}" -password "${UNITY_PASSWORD}" \
    -logFile /tmp/unity_license.log || true
  echo "[start] Done (see /tmp/unity_license.log)."
else
  echo "[start] No Unity license secret set."
  echo "[start] Set UNITY_LICENSE (Personal .ulf contents) or UNITY_EMAIL+UNITY_PASSWORD+UNITY_SERIAL (Plus/Pro)."
  echo "[start] Until then the EditMode suite reports 'No valid Unity Editor license found'."
fi
