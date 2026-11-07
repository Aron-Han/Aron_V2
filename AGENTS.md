# Version publishing and historical maintenance

- Only increment BuildVersion.txt when the user explicitly asks to update the version. Refresh build date on every build.
- Every requested version update MUST finish with a verified GitHub push to origin, including the exact source commit, an annotated immutable `VerX.XX.XXX-yyyyMMdd_HHmmss` tag, and ReleaseRecords build hashes. Never report upload success before checking the remote tag SHA.
- Build and test first in an isolated artifacts directory when Debug is running. Never terminate the application or overwrite field Project/config/VPP data.
- Use ReleaseTools/Publish-Version.ps1 with the verified build output. Review staged changes, new source files and ReleaseSupport for credentials/customer data before pushing. Update its explicit new-source list when adding files; do not omit newly implemented source.
- Repository root is this folder. Required sibling solution, build, plugin and test source is archived by the publisher under ReleaseSupport. Never archive field configurations, logs, images, VPPs, credentials, or unreviewed runtime folders.
- Never force-push, move a published tag, rewrite history, or merge divergent master automatically. Publish independent release branches.
- If upload fails, state exactly what succeeded and what remains pending; retain the local commit/tag for retry without rebuilding or incrementing again.
- Historical requests: fetch GitHub tags, locate exact version AND build date via ReleaseRecords, use ReleaseTools/Open-HistoricalVersion.ps1 to create a separate hotfix worktree. Do not downgrade the current workspace. Fix and test on that historical branch, then port the fix to current development separately.
- VisionPro 9.6 SR2 and existing camera/PLC workflow must remain unchanged unless explicitly requested.
