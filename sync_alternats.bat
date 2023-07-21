cd %~dp0
@if %errorlevel% neq 0 exit /b %errorlevel%

cd AlterNats
@if %errorlevel% neq 0 exit /b %errorlevel%

rem rd /q /s AlterNats
rem @if %errorlevel% neq 0 exit /b %errorlevel%

rem mkdir AlterNats
rem @if %errorlevel% neq 0 exit /b %errorlevel%


robocopy ..\..\AlterNats\src\AlterNats AlterNats -mir -xd bin obj
@if %errorlevel% geq 8 exit /b %errorlevel%
