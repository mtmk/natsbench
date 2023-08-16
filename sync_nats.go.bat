cd %~dp0
@if %errorlevel% neq 0 exit /b %errorlevel%

robocopy ..\nats.go .\nats.go -mir -xd .git .github
@if %errorlevel% geq 8 exit /b %errorlevel%
