@echo off
chcp 65001 > nul
title ConSecOrg Client
cd /d "%~dp0Client"
ConSecOrg.Client.exe
