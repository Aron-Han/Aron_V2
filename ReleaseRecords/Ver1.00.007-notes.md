# Ver1.00.007

Exact build timestamp, artifact SHA256 hashes and tag are in the adjacent version-buildDate JSON manifest. Field VPP, configurations, images and logs are excluded from source archival.

- Database menu now offers inspection records and a separate persistent operation audit, including user/time/action, filtering, CSV export and sensitive-value exclusion.
- General offers configurable integer-day Project ZIP backups with enable, interval, retention and destination settings. Backup state/logs stay outside Project. Cleanup is limited to this feature's expired archives.
- General now uses light feature cards with enable checkboxes, a fixed-on Display/Application checkbox, parameter draft dialogs and a vertical scrolling area. Cancel discards dialog changes; main Save remains the apply point. Chinese/English captions are supported.
- Manual menu uses the approved standard palm vector icon. Rotate Center button uses Calculate / 计算.
- VisionPro and plugin warnings/errors use Diagnostics instead of dedicated VisionPro/Plugins log writers. Historical local log deletion was blocked by automatic execution review; no historical-log deletion is claimed.
- SDK test eligibility uses effective Online state rather than the Online request alone. Recovery/switching/busy reasons are distinct and localized; busy channel numbers are reported. SDK test leases protect channel gates until completion/failure without changing PLC signals or transport selection.
- Project backup sources have evaluation-time fallback compile includes to prevent missing-source errors after stale project saves.

Validation: clean x64 Debug build; SdkOfflineGuardTests (fake communication, zero PLC calls); OperationsBackupTests; GeneralFeatureCardsTests (language, draft cancellation/confirmation, active-setting isolation, dirty state and vertical scrolling); BackupSourceIncludeTests. No field device startup was performed. The SDK correction still requires confirmation against the affected field installation.
