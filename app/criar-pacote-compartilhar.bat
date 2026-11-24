@echo off
rem Cria uma copia do app SEM seus dados pessoais, para colocar no pendrive.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ferramentas\criar-pacote.ps1"
echo.
pause
