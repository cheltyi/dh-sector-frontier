@echo off
cd ../../

call git submodule update --init --recursive
call dotnet build -p:WarningLevel=0

pause
