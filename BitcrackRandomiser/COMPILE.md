- The client is a .NET 8 console app (BitcrackRandomiser/BitcrackRandomiser.csproj:4-24), so you can produce native bundles for each OS with dotnet publish.
- From the repo root run:
    - dotnet publish BitcrackRandomiser/BitcrackRandomiser.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
    - dotnet publish BitcrackRandomiser/BitcrackRandomiser.csproj -c Release -r linux-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o publish/linux-x64
- Each publish folder contains the platform-specific executable plus the bundled miner binaries from the Apps directory (BitcrackRandomiser/BitcrackRandomiser.csproj:32-59); zip each output directory as the release artifact for that
OS.
- If you also ship the backend coordinator, publish it the same way for each target runtime: dotnet publish Backend/BitcrackPoolBackend/BitcrackPoolBackend.csproj -c Release -r linux-x64 --self-contained true -o publish/backend-linux
(guidance already in Backend/README.md:192-193).
- After publishing, test the binaries on the target OS (or in containers/VMs) and update the GitHub release with the zipped folders plus the settings.example.txt.