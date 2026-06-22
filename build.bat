@echo off
dotnet build src/addon-modules/DotNetFM.Module.Windows/DotNetFM.Module.Windows.csproj -c Release && dotnet publish dot-net-fm.csproj -c Release -r win-x64