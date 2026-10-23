@echo off
rem Fake adb for PersistentAdbShell tests.
rem mode.txt next to this file selects behaviour: ok (default) | slow | fail.
rem Every launch appends one line to spawns.log so tests can count processes.
setlocal EnableDelayedExpansion
set "DIR=%~dp0"
set "ARGS=%*"
>>"%DIR%spawns.log" echo !ARGS!

if not "!ARGS:kill-server=!"=="!ARGS!" exit /b 0
if not "!ARGS:start-server=!"=="!ARGS!" exit /b 0

set "MODE=ok"
if exist "%DIR%mode.txt" set /p MODE=<"%DIR%mode.txt"

if /I "!MODE!"=="fail" goto :fail
if /I "!MODE!"=="slow" goto :slow
goto :ok

:ok
set "LINE="
set /p "LINE="
if errorlevel 1 (
    >>"%DIR%exited.txt" echo exit
    exit /b 0
)
echo !LINE!
echo OUTPUT-OK
echo mybot.run:
goto :ok

:slow
set "LINE="
set /p "LINE="
if errorlevel 1 (
    >>"%DIR%exited.txt" echo exit
    exit /b 0
)
ping -n 2 127.0.0.1 >nul
echo !LINE!
echo SLOW-OK
echo mybot.run:
goto :slow

:fail
set "LINE="
set /p "LINE="
if errorlevel 1 (
    >>"%DIR%exited.txt" echo exit
    exit /b 0
)
echo !LINE!
if /I "!LINE!"=="export PS1=mybot.run:" (
    echo mybot.run:
)
goto :fail
