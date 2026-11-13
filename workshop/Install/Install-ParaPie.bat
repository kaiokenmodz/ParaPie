@echo off
setlocal
title ParaPie installer
echo.
echo  ParaPie - Sims-style pie menu for Paralives
echo  ===========================================
echo  This copies the ParaPie mod and the BepInEx mod loader into your Paralives game folder.
echo.
tasklist /FI "IMAGENAME eq Paralives.exe" 2>nul | find /I "Paralives.exe" >nul
if not errorlevel 1 (
  echo  Paralives is running. Please close the game first, then run this again.
  pause
  exit /b 1
)
rem The Workshop folder lives in the same Steam library as the game:
rem   steamapps\workshop\content\1118520\<item>\<mod>\Install  ->  steamapps\common\Paralives
set "GAME=%~dp0..\..\..\..\..\..\common\Paralives"
if exist "%GAME%\Paralives.exe" goto found
set "GAME=%ProgramFiles(x86)%\Steam\steamapps\common\Paralives"
if exist "%GAME%\Paralives.exe" goto found
echo  Could not find Paralives automatically.
echo  In Steam: right-click Paralives ^> Manage ^> Browse local files, copy that folder's path,
set /p "GAME= and paste it here, then press Enter: "
set "GAME=%GAME:"=%"
if not exist "%GAME%\Paralives.exe" (
  echo  Paralives.exe was not found in "%GAME%". Nothing was changed.
  pause
  exit /b 1
)
:found
for %%I in ("%GAME%") do set "GAME=%%~fI"
echo  Installing into: %GAME%
xcopy "%~dp0Files\*" "%GAME%\" /E /I /Y /Q >nul
if errorlevel 1 (
  echo  Copying failed. Try right-clicking this file and choosing "Run as administrator".
  pause
  exit /b 1
)
echo.
echo  Done! Start Paralives and click another Para to open the pie.
echo  Keys: Ctrl+F7 on/off, Ctrl+F9 unstick. After a Workshop update, run this installer again.
echo.
pause
