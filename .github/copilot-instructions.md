# Copilot Instructions

## Project Guidelines
- When testing changes to the OptiGraphMigrator global tool, must repack (dotnet pack) and reinstall the global tool (uninstall/install), and clear the NuGet cache if needed, since running from source alone doesn't update the installed CLI the user actually invokes.