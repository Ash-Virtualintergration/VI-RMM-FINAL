# Vi-RMM — Live Endpoint Management Console & Windows Agent

## Architecture Overview

Vi-RMM is a full-featured, real-time Remote Monitoring & Management (RMM) system with live SignalR endpoint communication.

### Projects
1. **`Vi-RMM`**: ASP.NET Core Blazor Interactive Web Console with real-time SignalR Hub (`/agentHub`).
2. **`Vi-RMM.Agent`**: Self-contained Windows Service & interactive endpoint agent.
3. **`Vi-RMM.Shared`**: Strongly-typed SignalR DTOs (Telemetry, Live Terminal, File Manager, Processes, Services, Screen Frames, Input Simulation, Health Checks).

---

## Quick Start Guide

### 1. Start the Vi-RMM Web Console
Open `Vi-RMM.sln` in Visual Studio 2022 / VS Code, or run:
```powershell
dotnet run --project Vi-RMM/Vi-RMM.csproj
```

**Development credentials:**
- Username: `admin`
- Password: `admin`

These credentials work only with `ASPNETCORE_ENVIRONMENT=Development`. Outside Development, set the `ViRMM__AdminPassword` environment variable. This sample app is not production-secure; do not expose it publicly or connect real endpoints until proper authentication and agent-hub authorization are implemented.

---

### 2. Download and Deploy the Agent

#### Method A: Web UI Download
1. Navigate to **Agents > Install Agent** (`/agents/install`).
2. Click **Download Vi-RMM.Agent.exe** (already compiled and ready in `wwwroot/downloads/`).
3. Run as Administrator on target Windows endpoints:
   ```powershell
   .\Vi-RMM.Agent.exe --install --server https://your-rmm-host
   ```

#### Method B: One-Liner PowerShell Deployment
Run directly on target machines (as Administrator). Replace the server URL with the address of the running web console. `localhost` only works when the web console is running on the same machine as the agent:
```powershell
powershell -ExecutionPolicy Bypass -Command "$server='https://your-rmm-host'; $dest=\"$env:ProgramFiles\Vi-RMM\"; New-Item -ItemType Directory -Force -Path $dest | Out-Null; Invoke-WebRequest -Uri \"$server/downloads/Vi-RMM.Agent.exe\" -OutFile \"$dest\Vi-RMM.Agent.exe\"; & \"$dest\Vi-RMM.Agent.exe\" --install --server $server"
```

---

## Live Features Available on Dashboard

- **⚡ Real-Time Endpoint Telemetry**: Continuous sub-second CPU, RAM, Disk, Uptime, and Process metrics.
- **💻 Live Command Terminal**: High-contrast interactive PowerShell & CMD execution with live output streaming.
- **🖥️ Remote Desktop**: Live desktop screen viewer with quality controls and mouse/keyboard input control.
- **📁 Remote File Manager**: Browse remote drives/folders, download files, upload files, delete items, and preview files.
- **⚙️ Process Manager**: Real-time process listing with memory/CPU meters and process kill capability.
- **🔧 Services Manager**: Windows Services inspector with Start, Stop, and Restart controls.
- **🛡️ Automated Health Checks**: Evaluates Windows Defender, Firewall, Storage capacity, and OS health.
- **📜 Script Runner & Automation**: Deploy pre-built PowerShell maintenance scripts across endpoints.
- **📋 Audit Logging**: Comprehensive activity tracking of all operator actions and remote commands.
- **📊 Reporting**: Executive health summary, patch compliance reports, and hardware/software inventory export.

---

## Re-Publishing the Agent
To re-publish the single-file agent at any time, run:
```powershell
powershell -File Vi-RMM.Agent/Publish-ViRMM-Agent.ps1
```
