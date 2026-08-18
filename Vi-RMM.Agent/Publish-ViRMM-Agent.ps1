dotnet publish "$PSScriptRoot\Vi-RMM.Agent.csproj" -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o "$PSScriptRoot\..\Vi-RMM\wwwroot\downloads\"
Write-Host "Vi-RMM Agent successfully published to Vi-RMM\wwwroot\downloads\Vi-RMM.Agent.exe" -ForegroundColor Green
