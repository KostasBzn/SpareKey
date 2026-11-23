@echo off
rem ============================================================
rem  SpareKey - build for WINDOWS, SELF-CONTAINED
rem  .NET is packed inside. Runs on any 64-bit Windows, nothing to install.
rem  Output: publish\win-x64-self\SpareKey.exe
rem ============================================================

rem %~dp0 = the folder this script is in (builders\).
rem So the paths work no matter where you start the script from.
set "PROJECT=%~dp0..\SpareKey\SpareKey.csproj"
set "OUTPUT=%~dp0..\publish\win-x64-self"

if exist "%PROJECT%" goto build
echo Project not found:
echo %PROJECT%
echo Fix the PROJECT line at the top of this script.
goto end

:build
rem Delete the old build so no leftover files get mixed in
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

dotnet publish "%PROJECT%" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT%"
if errorlevel 1 goto failed

echo.
echo DONE. Your file is in:
echo %OUTPUT%
echo Double-click SpareKey.exe to run it.
goto end

:failed
echo.
echo BUILD FAILED - read the errors above.

:end
echo.
pause
