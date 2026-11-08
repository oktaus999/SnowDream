@echo off
cd /d "%~dp0"
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set OPTS=/nologo /target:winexe /unsafe /optimize+ /codepage:65001 /platform:x64 /r:System.Windows.Forms.dll /r:System.Drawing.dll
%CSC% %OPTS% /out:SnowDream.exe Snow.cs Ui.cs || goto :err
"%~dp0SnowDream.exe" --export-icon snow.ico || goto :err
%CSC% %OPTS% /win32icon:snow.ico /out:SnowDream.exe Snow.cs Ui.cs || goto :err
echo BUILD OK: SnowDream.exe
exit /b 0
:err
echo BUILD FAILED
exit /b 1
