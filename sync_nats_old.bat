cd %~dp0
@if %errorlevel% neq 0 exit /b %errorlevel%

cd nats_old
@if %errorlevel% neq 0 exit /b %errorlevel%

rem rd /q /s nats.net.v2
rem @if %errorlevel% neq 0 exit /b %errorlevel%

rem mkdir nats.net.v2\src\NATS.Client.Core
rem @if %errorlevel% neq 0 exit /b %errorlevel%

robocopy ..\..\nats.net.v2\src\NATS.Client.Core nats.net.v2\src\NATS.Client.Core -mir -xd bin obj
@if %errorlevel% geq 8 exit /b %errorlevel%

