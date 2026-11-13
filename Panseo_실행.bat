@echo off
chcp 65001 > nul
title Panseo 판서 도구 실행기
cd /d "%~dp0"
if exist "Panseo_포터블.exe" (
  start "" "Panseo_포터블.exe"
) else if exist "Panseo.exe" (
  start "" "Panseo.exe"
) else (
  start "" "bin\Release\Panseo.exe"
)
