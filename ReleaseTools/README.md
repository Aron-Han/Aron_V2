# Source releases and historical fixes

The repository is https://github.com/Aron-Han/Aron_V2 (currently public).

## When the user requests a version update

1. Increment BuildVersion.txt only on that explicit request. Build to a new workspace artifacts directory using the installed VS MSBuild and licensed VisionPro 9.6 SR2. Run relevant tests without starting cameras or PLC communication.
2. Run `ReleaseTools/Publish-Version.ps1 -BuildOutput <absolute verified output> -PrepareOnly`. Review source changes and ReleaseSupport. No VPP, field configuration, user credentials, images, logs or crash dumps belong on GitHub.
3. Run the same command without PrepareOnly. It validates EXE/source version and date, records SHA256 hashes, captures sibling sources, commits on a unique release branch, creates an annotated tag, atomically pushes branch/tag and verifies the remote tag commit. It does not rewrite master.
4. Report the exact tag URL and local artifact directory. Never claim the normal Debug EXE was replaced when it was locked. Keep matching EXE/PDB locally; GitHub records hashes, not field binaries. Treat a failed upload as pending, not published.

Tags are `Ver1.00.005-20261107_220645`, so identical software version numbers with different build dates remain distinguishable. ReleaseRecords is searchable by both fields. Published tags are immutable. Re-publishing requires a new build date; retrying a failed push uses its existing commit/tag without rebuilding.

## Retrieve a historical version

Fetch tags and inspect ReleaseRecords, then run:

```powershell
./ReleaseTools/Open-HistoricalVersion.ps1 -Tag Ver1.00.005-20261107_220645 -Destination D:\AronHotfix\case001
```

This creates a separate hotfix branch/worktree and restores sibling Build, PluginContracts, Plugins, Tests and solution files from ReleaseSupport. The current workspace is untouched. Restore NuGet packages and provide matching licensed external dependencies (VisionPro, HslCommunication and any local SunnyUI references); machine-specific absolute reference paths may need setup. The manifest records binary hashes but is not a complete installer or a guarantee of identical build bytes on another machine.

Diagnose using the field version/build date, diagnostic package, exact EXE/PDB and relevant configuration/VPP supplied privately. Fix on the historical branch, validate compatibility, publish a new immutable patch tag, and port the fix to current development separately.

The first new baseline does not retrospectively recreate older source snapshots. Existing old Git commits can be inspected, but cannot be claimed to match a field build without evidence.
