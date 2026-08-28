#!/usr/bin/env bash
# Idempotent Cloud Agent bootstrap for Myriad of Dragons.
# Installs the Unity 6000.5.6f1 Linux editor + its runtime libraries and the Node server deps.
# Safe to re-run: every step is guarded so a warm/cached VM converges without redoing work.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

UNITY_VERSION="6000.5.6f1"
# Changeset from ProjectSettings/ProjectVersion.txt (m_EditorVersionWithRevision).
UNITY_CHANGESET="0e0577a1a2ac"
UNITY_ROOT="/opt/unity/${UNITY_VERSION}"
UNITY_BIN="${UNITY_ROOT}/Editor/Unity"

echo "== MOD install: system libraries Unity needs on headless Linux =="
export DEBIAN_FRONTEND=noninteractive
sudo apt-get update -qq
sudo apt-get install -y --no-install-recommends \
  xvfb libgtk-3-0 libnss3 libasound2t64 libxtst6 libxss1 libglu1-mesa libgl1 libgbm1 \
  libnotify4 libunwind8 libssl3 zlib1g libc6-dev libncurses6 libxrandr2 libxcursor1 \
  libxi6 libxcomposite1 libxdamage1 libxrender1 libxext6 libx11-6 libcanberra-gtk3-module \
  libatk1.0-0 libatk-bridge2.0-0 libcups2 libpango-1.0-0 libpangocairo-1.0-0 \
  libgdk-pixbuf-2.0-0 libdrm2 libxfixes3 ca-certificates curl

echo "== MOD install: Unity ${UNITY_VERSION} editor =="
if [ -x "${UNITY_BIN}" ] && "${UNITY_BIN}" -version >/dev/null 2>&1; then
  echo "Unity already present at ${UNITY_BIN} ($(${UNITY_BIN} -version 2>/dev/null))"
else
  sudo mkdir -p "${UNITY_ROOT}" /opt/unity/download
  sudo chown -R "$(id -u):$(id -g)" /opt/unity
  echo "Downloading Unity editor (~4.4 GB, one time)..."
  curl -L --retry 5 --retry-delay 4 -C - -o /opt/unity/download/Unity.tar.xz \
    "https://download.unity3d.com/download_unity/${UNITY_CHANGESET}/LinuxEditorInstaller/Unity.tar.xz"
  echo "Extracting..."
  tar -xf /opt/unity/download/Unity.tar.xz -C "${UNITY_ROOT}"
  rm -f /opt/unity/download/Unity.tar.xz
  "${UNITY_BIN}" -version
fi

echo "== MOD install: Node server dependencies =="
if [ -f "${REPO_ROOT}/Server/package-lock.json" ]; then
  (cd "${REPO_ROOT}/Server" && npm ci)
else
  (cd "${REPO_ROOT}/Server" && npm install --no-audit --no-fund)
fi

echo "== MOD install: complete =="
