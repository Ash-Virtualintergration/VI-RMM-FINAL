# Vi-RMM — Final Project Handoff

Open `Vi-RMM.sln` in Visual Studio 2022.

## Projects

1. `Vi-RMM` — ASP.NET Core Blazor web console
2. `Vi-RMM.Agent` — Windows service agent foundation

## First run

1. Open the solution.
2. Set `Vi-RMM` as the startup project.
3. Build > Rebuild Solution.
4. Run.

Demo account:
- Username: `admin`
- Password: `ChangeMe123!`

## Agent EXE

Publish from the `Vi-RMM.Agent` project with:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true
```

Copy the resulting `Vi-RMM.Agent.exe` to:

`Vi-RMM\wwwroot\downloads\Vi-RMM.Agent.exe`

The dashboard's **Agents > Install Agent** page then provides the download.

## Current milestone

The project contains the original Vi-RMM UI foundation, dashboard, profile menu, user administration UI, roles, password reset UI, profile picture support, dark/light mode, sign-out flow, agent deployment page and Windows-service agent foundation.

The next production milestone should replace the in-memory `UserStore` with a real database and ASP.NET Core Identity, then add authenticated agent registration, secure real-time communications and the RMM feature modules.
