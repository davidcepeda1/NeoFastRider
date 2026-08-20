#!/usr/bin/env bash
# Abre este proyecto con el Editor de Unity correcto para cada plataforma.
#
# AVISO: en Linux, renderizar el HUD de NoesisGUI (Assets/NoesisGUI/Plugins/
# Libraries/Linux/x86_64/libNoesis.so) crashea el Editor (SIGSEGV, null-deref
# dentro de libgallium/libNoesis). Se probaron y DESCARTARON como fix:
# -force-vulkan (evita el crash pero rompe el HUD, Noesis Linux no tiene
# backend Vulkan), -force-gfx-direct, -force-glcore y RADEONSI_DEBUG=llvm
# (ninguno evita el crash). Ver memoria de proyecto "noesis_linux_crash_fix"
# para el detalle. Este script NO aplica ningun flag magico porque no se
# encontro ninguno que funcione; se deja preparado para cuando se resuelva
# (probablemente requiere actualizar el plugin NoesisGUI). En Windows el
# Editor usa DirectX por defecto y no tiene este problema.

set -euo pipefail

PROJECT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSION="$(grep -o 'm_EditorVersion: .*' "$PROJECT_DIR/ProjectSettings/ProjectVersion.txt" | cut -d' ' -f2)"

UNITY_BIN="$HOME/Unity/Hub/Editor/$VERSION/Editor/Unity"
if [[ ! -x "$UNITY_BIN" ]]; then
    echo "No se encontro el Editor Unity $VERSION en $UNITY_BIN" >&2
    echo "Instalalo desde Unity Hub o ajusta UNITY_BIN en este script." >&2
    exit 1
fi

exec "$UNITY_BIN" -projectpath "$PROJECT_DIR" "$@"
