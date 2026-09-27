# Contributing

Build and run the checks described in [README.md](README.md). Use 64-bit Windows PowerShell 5.1 for the test runner. Changes should include focused regression coverage where possible.

## Runtime constraints

- Preserve executable identity and native hook validation, and both campaign restrictions. Do not reject a game solely by its version number. Unknown or unreadable eligibility must reject application.
- Do not test changes in multiplayer or other non-campaign sessions. Test eligibility rejection with the local mocks.
- HUD changes must remain reversible, must not accumulate scaling across updates, and must validate frame identity before restoration.
- Call UI operations through the normal game UI path. Retain native allocations until game exit if callbacks could still be executing.
- Do not commit game binaries, memory dumps, extracted game assets, account details, machine-specific paths, or generated logs.

The launcher targets .NET Framework and the engine compiles with Windows PowerShell's C# compiler. Avoid language features that those compilers do not support. Preserve UTF-8 BOM encoding for PowerShell scripts so Windows PowerShell reads non-ASCII text consistently.

## Reports

Include the helper version, game build, campaign/mission, resolution, HUD size, and steps to reproduce. A cropped screenshot and relevant activity-log excerpt are useful. Remove account information and personal filesystem paths before posting.

## Release checklist

1. Update the assembly version in `Launcher.cs` and `CHANGELOG.md`.
2. Run `Tests/Run.ps1`, including the interactive layout checks on a desktop.
3. Verify campaign application, selection changes, scale changes, restoration, and Alt+Tab in supported campaign missions.
4. Run `Release.ps1`. Review both ZIP contents and `SHA256SUMS.txt`.
5. Publish the tested ZIPs and checksums through GitHub Releases with the matching version tag. Do not claim untested game builds or campaign layouts are supported.
