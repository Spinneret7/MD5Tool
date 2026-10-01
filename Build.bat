@echo off
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
"%CSC%" /nologo /target:winexe /win32icon:icon.ico /optimize+ /out:Md5Tool.exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll Md5Tool.cs
if errorlevel 1 (echo Build failed.) else (echo Built Md5Tool.exe)
pause