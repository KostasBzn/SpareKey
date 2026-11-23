@echo off
rem ============================================================
rem  SpareKey - build for LINUX, SELF-CONTAINED
rem  .NET is packed inside. Runs on any 64-bit Linux, nothing to install.
rem  Output: publish\linux-x64-self\SpareKey
rem ============================================================

rem %~dp0 = the folder this script is in (builders\).
rem So the paths work no matter where you start the script from.
set "PROJECT=%~dp0..\SpareKey\SpareKey.csproj"
set "OUTPUT=%~dp0..\publish\linux-x64-self"

if exist "%PROJECT%" goto build
echo Project not found:
echo %PROJECT%
echo Fix the PROJECT line at the top of this script.
goto end

:build
rem Delete the old build so no leftover files get mixed in
if exist "%OUTPUT%" rmdir /s /q "%OUTPUT%"

dotnet publish "%PROJECT%" -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o "%OUTPUT%"
if errorlevel 1 goto failed

echo.
echo DONE. Your file is in:
echo %OUTPUT%
echo On Linux, run once:  chmod +x SpareKey
goto end

:failed
echo.
echo BUILD FAILED - read the errors above.

:end
echo.
pause
