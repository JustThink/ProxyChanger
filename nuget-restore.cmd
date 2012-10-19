@echo off

setlocal enableextensions
set EnableNuGetPackageRestore=true

if "%1" == "" (set src=%CD%) else (set src=%1)

for /r "%src%" %%c in (packages.config) do if exist "%%c" nuget.exe install "%%c" -o packages

pause