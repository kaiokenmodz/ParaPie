@echo off
setlocal
title ParaPie uninstaller
set "GAME=%~dp0..\..\..\..\..\..\common\Paralives"
if not exist "%GAME%\Paralives.exe" set "GAME=%ProgramFiles(x86)%\Steam\steamapps\common\Paralives"
if not exist "%GAME%\Paralives.exe" set /p "GAME=Paste your Paralives folder path and press Enter: "
set "GAME=%GAME:"=%"
if not exist "%GAME%\Paralives.exe" ( echo Paralives.exe not found. Nothing was changed. & pause & exit /b 1 )
for %%I in ("%GAME%") do set "GAME=%%~fI"
echo.
echo  1 = remove only ParaPie (keep BepInEx for other mods)
echo  2 = remove ParaPie AND BepInEx (game back to stock)
choice /C 12 /N /M " Choose 1 or 2: "
if errorlevel 2 goto all
rmdir /S /Q "%GAME%\BepInEx\plugins\ParaPie" 2>nul
echo  ParaPie removed.
pause
exit /b 0
:all
rmdir /S /Q "%GAME%\BepInEx" 2>nul
del /Q "%GAME%\winhttp.dll" "%GAME%\doorstop_config.ini" "%GAME%\.doorstop_version" 2>nul
echo  ParaPie and BepInEx removed. Paralives is back to stock.
pause
