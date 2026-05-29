# GitDelta — Release Verification Checklist

Run this before publishing a GitHub Release. Prefer a **clean Windows 11 x64 VM** (or a fresh
user profile) so runtime detection and the PATH broadcast are genuinely exercised.

## A. Build artifacts (build machine)

- [ ] **MANDATORY:** obtain the official .NET 10 Desktop Runtime (x64) **SHA-256** from
      <https://dotnet.microsoft.com/download/dotnet/10.0> and pass it to the fetch script
      (`pwsh -File installer/fetch-runtime.ps1 -ExpectedSha256 <hash>`) so the bundled
      runtime is integrity-checked before it ships in the installer.
- [ ] `pwsh -File build/publish.ps1` succeeds; `publish/gitdelta.exe` exists and is ~40–50 MB.
- [ ] `publish/` contains no per-app managed `*.dll` siblings (single-file bundling confirmed).
- [ ] `& publish/gitdelta.exe --version` prints the expected version and exits without a window.
- [ ] `& publish/gitdelta.exe --version > out.txt` writes the version to the file (redirection works).
- [ ] `pwsh -File build/make-installer.ps1` produces `installer/Output/GitDelta-<version>-setup.exe`.

## B. Clean-machine install (test VM WITHOUT .NET 10 Desktop Runtime and WITHOUT gitdelta on PATH)

- [ ] Copy `GitDelta-<version>-setup.exe` to the clean VM and run it (standard, non-admin user).
- [ ] SmartScreen shows "Unknown publisher"; **More info > Run anyway** proceeds (matches README).
- [ ] Installer runs **without a UAC elevation prompt** (per-user install).
- [ ] If the bundled runtime is present: installer shows "Installing the .NET 10 Desktop Runtime..."
      and completes; if not bundled: the .NET download page opens at the end.
- [ ] App installs to `%LOCALAPPDATA%\Programs\GitDelta` and `gitdelta.exe` is present there.
- [ ] A **Start Menu** "GitDelta" shortcut exists and launches the app to the start screen.
- [ ] **Settings > Apps > Installed apps** lists "GitDelta" with the correct version/publisher.

## C. PATH integration (test VM)

- [ ] Open a **brand-new** PowerShell window after install (do not reuse a pre-install terminal).
- [ ] `gitdelta --version` prints the version (resolves via PATH).
- [ ] `gitdelta --help` prints usage to the terminal.
- [ ] `gitdelta --version > out.txt` writes the version to the file (redirection works).
- [ ] In a folder that IS a git repo, run `gitdelta` — app opens to **working-tree-vs-HEAD**.
- [ ] In a folder that is NOT a git repo, run `gitdelta` — app opens to the **start screen**.
- [ ] `gitdelta <path-to-repo>` opens that repo's working-tree view.
- [ ] Inspect the PATH entry is a single, non-duplicated value:
      `(Get-ItemProperty 'HKCU:\Environment' Path).Path` contains `...\Programs\GitDelta` exactly once.

## D. Idempotent re-install (test VM)

- [ ] Run the installer a second time over the existing install; it completes cleanly.
- [ ] PATH still contains `...\Programs\GitDelta` **exactly once** (no duplicate appended).

## E. Uninstall (test VM)

- [ ] Uninstall via Settings > Apps (or the Start Menu "Uninstall GitDelta" shortcut).
- [ ] `%LOCALAPPDATA%\Programs\GitDelta` is removed.
- [ ] Open a new terminal: `gitdelta` is no longer found (PATH entry removed).
      Confirm: `(Get-ItemProperty 'HKCU:\Environment' Path).Path` no longer contains the GitDelta dir.

## F. Per-user .NET runtime (test VM with a USER-scoped .NET 10 Desktop Runtime install)

The installer's runtime probe only checks the machine-wide location
(`{commonpf64}\dotnet\shared\Microsoft.WindowsDesktop.App\10.*`). A runtime installed to the
**per-user** location (`%LOCALAPPDATA%\Microsoft\dotnet`) is NOT detected, so the bundled
runtime installer will run even though a usable runtime is already present.

- [ ] Install the .NET 10 Desktop Runtime per-user only (`%LOCALAPPDATA%\Microsoft\dotnet`),
      then run the GitDelta installer.
- [ ] Confirm the resulting behavior is acceptable: the bundled runtime runs unnecessarily but
      the install still completes and `gitdelta` works. (If this becomes a real complaint,
      extend `IsDotNet10DesktopRuntimeInstalled` in `GitDelta.iss` to also probe the per-user
      location and `dotnet --list-runtimes`.)

## G. Git-missing path (optional, test VM without Git for Windows)

- [ ] Launch GitDelta with git absent — the actionable "Git not installed" screen appears with a
      link to Git for Windows (per the spec's startup git detection).

Only publish the release once every box in A–E passes.
