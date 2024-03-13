@echo off
setlocal

powershell -ExecutionPolicy ByPass -NoProfile -Command "& '%~dp0startcode.ps1'" %*
