# Vi-RMM Agent

Windows endpoint agent foundation.

Publish a self-contained Windows x64 executable:

dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true

The published EXE can later be placed at:

Vi-RMM/wwwroot/downloads/Vi-RMM.Agent.exe

This milestone includes Windows Service installation and a local heartbeat. Server registration, real-time telemetry, remote shell, file management, remote control, patching and other RMM functions are intentionally separate implementation milestones.
