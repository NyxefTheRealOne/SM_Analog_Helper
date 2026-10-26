@echo off
title Analog Controller Input - Controller Helper
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Controller\Start-Controller.ps1"
if errorlevel 1 pause
