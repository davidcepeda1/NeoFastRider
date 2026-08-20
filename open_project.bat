@echo off
REM Abre este proyecto con el Editor de Unity correcto para cada plataforma.
REM
REM En Windows el Editor usa DirectX por defecto, que no tiene el bug de
REM NoesisGUI que sí ocurre en Linux con OpenGL/Mesa, asi que aca no se
REM necesita ningun flag extra. Este script solo existe para que el flujo
REM de apertura sea el mismo en cualquier sistema operativo (ver open_project.sh
REM para la version Linux, que fuerza -force-vulkan).

setlocal
set "PROJECT_DIR=%~dp0"
for /f "tokens=2" %%v in ('findstr /r "m_EditorVersion:" "%PROJECT_DIR%ProjectSettings\ProjectVersion.txt"') do set "VERSION=%%v"

set "UNITY_BIN=%USERPROFILE%\Unity\Hub\Editor\%VERSION%\Editor\Unity.exe"
if not exist "%UNITY_BIN%" (
    echo No se encontro el Editor Unity %VERSION% en %UNITY_BIN%
    echo Instalalo desde Unity Hub o ajusta UNITY_BIN en este script.
    exit /b 1
)

start "" "%UNITY_BIN%" -projectPath "%PROJECT_DIR%"
