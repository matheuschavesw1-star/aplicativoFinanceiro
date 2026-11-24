@echo off
rem Recompila o Orcamento.exe a partir de Launcher.cs usando o compilador que ja vem no Windows (nao precisa instalar nada).
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
"%CSC%" /nologo /target:winexe /optimize+ /out:"%~dp0..\Orcamento.exe" /win32icon:"%~dp0..\icons\app.ico" /r:System.Windows.Forms.dll /r:System.dll "%~dp0Launcher.cs"
if errorlevel 1 ( echo Falha ao compilar. ) else ( echo Orcamento.exe recompilado. )
pause
