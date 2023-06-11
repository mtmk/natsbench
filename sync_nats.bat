cd %~dp0
@if %errorlevel% neq 0 exit /b %errorlevel%

cd nats
@if %errorlevel% neq 0 exit /b %errorlevel%

rd /q /s nats.net.v2
@if %errorlevel% neq 0 exit /b %errorlevel%

mkdir nats.net.v2\src\NATS.Client.Core
@if %errorlevel% neq 0 exit /b %errorlevel%

mkdir nats.net.v2\tests\NATS.Client.Core.Tests
@if %errorlevel% neq 0 exit /b %errorlevel%

robocopy ..\..\nats.net.v2\src\NATS.Client.Core nats.net.v2\src\NATS.Client.Core -mir -xd bin obj
@if %errorlevel% geq 8 exit /b %errorlevel%

robocopy ..\..\nats.net.v2\tests\NATS.Client.Core.Tests nats.net.v2\tests\NATS.Client.Core.Tests -mir -xd bin obj
@if %errorlevel% geq 8 exit /b %errorlevel%
