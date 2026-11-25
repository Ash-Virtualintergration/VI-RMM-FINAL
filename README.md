# Vi-RMM

Clean two-project foundation:

- Vi-RMM: Blazor/ASP.NET Core web console
- Vi-RMM.Agent: Windows service agent

## Current UI milestone

- Vi-RMM branding
- Tactical-style dense RMM layout with an original visual identity
- File/View/Agents/Settings/Tools/Reporting/Help navigation
- Top-right profile menu
- Manage Users
- Create users
- Roles: Administrator, Technician, Read Only
- Delete users
- Reset passwords
- Username/login assignment
- Profile picture upload
- Dark/light visual theme toggle
- Sign out
- Install Agent page and download link

## Important

User management and audit history are in-memory and reset when the application restarts. This is not production authentication. The login page redirects unauthenticated users to sign-in; the demo credentials `admin` / `admin` are enabled only in the Development environment. Outside Development, configure `ViRMM__AdminPassword` before starting the app.

Do not expose this RMM console to the public internet or connect real endpoints until authentication, authorization, persistent storage, and the agent hub are secured for production.

## Run

Open Vi-RMM.sln in Visual Studio 2022 with the .NET 10 SDK.

Build -> Rebuild Solution.

Run the Vi-RMM project.

Development login:
admin / admin
