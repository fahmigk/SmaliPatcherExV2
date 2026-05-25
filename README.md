# SmaliPatcherEx v2.0 — Android 16 (API 36)

## How to get the EXE (takes ~3 minutes)

1. Create a new **public** GitHub repo (e.g. `SmaliPatcherEx`)
2. Upload **all files** from this folder into that repo
3. Go to **Actions** tab → click the workflow → **Run workflow**
4. When it finishes → click the run → **Artifacts** → download `SmaliPatcherEx-Windows-x64.zip`
5. Unzip → run `SmaliPatcherEx.exe`

## Usage

1. Pull `services.jar` from device:
   ```
   adb pull /system/framework/services.jar
   ```
2. Open EXE → click **Select services.jar**
3. Set API level (36 for Android 16)
4. Optionally paste your device fingerprint:
   ```
   adb shell getprop ro.build.fingerprint
   ```
5. Select patches → click **Patch & Build Module**
6. Flash the output `SmaliPatcherEx-module.zip` via Magisk / KernelSU / APatch
7. Reboot

## Patches included

| Patch | Description | Android |
|-------|-------------|---------|
| mock_location_appops | Mock Location AppOps bypass | A10–A16 |
| mock_location_isprovider | isMockProvider always true | A12–A16 |
| mock_location_provider_manager | LocationProviderManager bypass | A13–A16 |
| mock_location_appops_helper | AppOpsHelper bypass | A14–A16 |
| mock_permission_dpm | DevicePolicyManager bypass | A5–A16 |
| mock_permission_restrictions | UserRestrictionsUtils bypass | A11–A16 |
| gnss_mock_provider | GnssManagerService bypass | A13–A16 |
| gnss_location_provider_legacy | GnssLocationProvider legacy | A9–A12 |
| signature_spoofing_pms | PackageManagerService | A5–A13 |
| signature_spoofing_computer | ComputerEngine (A14+ PM refactor) | A14–A16 |
| signature_spoofing_snapshot | Snapshot class | A14–A16 |
| no_permission_review | Skip REVIEW_REQUIRED flag | A10–A16 |
| doze_whitelist | DeviceIdleController whitelist | A6–A16 |
| untrusted_touch | InputManagerService bypass | A12–A16 |
| untrusted_touch_wms | WindowManagerService bypass | A12–A16 |
| overlay_any | Allow unsigned overlays | A10–A16 |
