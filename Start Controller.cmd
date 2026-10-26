@echo off
title Analog Drive - Xbox Controller
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Controller\Start-Controller.ps1"
if errorlevel 1 pause
