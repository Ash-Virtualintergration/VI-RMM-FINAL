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

User management in this demo milestone is in-memory. It is NOT production authentication. The next backend milestone should replace UserStore/SessionState with ASP.NET Core Identity plus a central database, hashed passwords, persistent roles/claims, audit logging and real session/sign-out enforcement.

## Run

Open Vi-RMM.sln in Visual Studio 2022 with the .NET 10 SDK.

Build -> Rebuild Solution.

Run the Vi-RMM project.

Demo login:
admin / ChangeMe123!
