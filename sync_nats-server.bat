cd %~dp0
@if %errorlevel% neq 0 exit /b %errorlevel%

robocopy ..\nats-server .\nats-server -mir -xd .git .github
@if %errorlevel% geq 8 exit /b %errorlevel%
