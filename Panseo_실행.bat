@echo off
chcp 65001 > nul
title Panseo 판서 도구 실행기
cd /d "%~dp0"
if exist "panseo_portable.exe" (
  start "" "panseo_portable.exe"
) else if exist "Panseo.exe" (
  start "" "Panseo.exe"
) else (
  start "" "bin\Release\Panseo.exe"
)
