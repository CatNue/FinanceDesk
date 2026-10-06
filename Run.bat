@echo off
cd /d "%~dp0"
echo Starting FinanceDesk (MySQL must be running)...
dotnet run --launch-profile FinanceDesk
pause
