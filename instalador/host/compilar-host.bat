@echo off
rem Compila o novo Orcamento.exe (WebView2) em host\bin\ usando o csc que ja vem no Windows (C# 5).
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
set "RAIZ=%~dp0"
set "APP=%~1"
if "%APP%"=="" set "APP=%RAIZ%..\..\controle-financeiro-teste"
if not exist "%RAIZ%bin" mkdir "%RAIZ%bin"
copy /y "%RAIZ%lib\*.dll" "%RAIZ%bin\" >nul
set "ICO=%APP%\icons\app.ico"
set ICOARG=
if exist "%ICO%" set ICOARG=/win32icon:"%ICO%"
"%CSC%" /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 %ICOARG% /out:"%RAIZ%bin\Orcamento.exe" ^
  /lib:"%RAIZ%lib" /r:Microsoft.Web.WebView2.Core.dll /r:Microsoft.Web.WebView2.WinForms.dll ^
  /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Web.Extensions.dll /r:System.dll /r:System.Core.dll ^
  "%RAIZ%Orcamento.cs"
if errorlevel 1 ( echo Falha ao compilar o host. & exit /b 1 ) else ( echo Orcamento.exe compilado em host\bin\ )
