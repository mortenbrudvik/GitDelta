# GitDelta — Design Spec

- **Date:** 2026-05-29
- **Status:** Approved (ready for implementation planning)
- **Type:** New Windows desktop application (greenfield)

---

## 1. Summary

GitDelta is a **lightweight, native Windows 11 desktop app for read-only Git diff and commit comparison**. It makes it effortless to see what changed between any two commits, what a single commit introduced, or what is currently uncommitted in the working tree — presented with a modern, native Fluent UI.

**North stars:** lightweight · native feel · effortless comparison.

**Non-goals (v1):** it is *not* a full Git client. No staging, committing, discarding, pushing, branching, or any write operation. Pure visualization.

---

## 2. Users & key scenarios

Primary user: a developer who wants a fast, clean way to inspect changes on Windows.

Key scenarios:
1. *"What have I changed but not committed yet?"* — run `gitdelta` in a repo from the terminal; it opens straight to the working-tree-vs-HEAD diff.
2. *"What did this commit change?"* — open a repo, click a commit, see it diffed against its parent.
3. *"What's different between these two commits?"* — select two commits and view the diff between them.

---

## 3. Compare modes

All three modes share one **changed-files list** and one **diff view**; they differ only in how the comparison is chosen.

1. **Working tree vs HEAD** — *combined* uncommitted view: staged + unstaged + untracked changes together. This is the default mode and the CLI fast-path target.
2. **Single commit vs parent** — selecting one commit shows the diff it introduced (against its first parent; against the empty tree for a root commit).
3. **Any two commits** — selecting two commits shows the diff between them.

Deferred to a future version: branch-vs-branch; staged/unstaged tri-state split.

---

## 4. Launch & CLI behavior

GitDelta is a hybrid GUI/CLI app. The installer puts `gitdelta.exe` on the user's `PATH`.

| Invocation | Behavior |
|---|---|
| `gitdelta` in a repo folder (terminal) | Resolve repo root via `git rev-parse --show-toplevel` from the terminal's cwd, open **working-tree-vs-HEAD**. |
| `gitdelta <path>` | Open the repo at `<path>` to working-tree-vs-HEAD. |
| Launch with no repo context (Start menu, or cwd is not a repo) | Show the **start screen** (brand + "Open folder" picker). |
| `gitdelta --help` / `--version` | Write to the calling terminal and exit. |

**Dual-mode console output:** the app is a `WinExe` (no console window flashes on GUI launch). For `--help`/`--version`, it calls `AttachConsole(ATTACH_PARENT_PROCESS)` and reopens stdout to `CONOUT$`, **guarded by a `GetFileType` check** so that `gitdelta --version > out.txt` and pipes still work. Argument routing happens in the IHost bootstrap before the main window is created.

**Repo discovery:** if no path arg is given, default to `Directory.GetCurrentDirectory()`. Treat "no path arg AND cwd is not a git repo" as the start-screen case (a Start-menu launch sets cwd to System32/app dir — do not diff that).

One repo open at a time (no tabs/multi-repo). A title-bar button opens a different folder.

---

## 5. UX & window layout (Layout A — unified 3-pane, history-driven)

A WPF-UI **`FluentWindow`** with Mica backdrop and an extended title bar containing: app name · current repo name · "Open folder" button · theme toggle.

Below the title bar, a three-pane workspace separated by draggable `GridSplitter`s:

- **Left — History** (virtualized list):
  - Pinned top row: **"● Working tree (uncommitted)"**.
  - Commit rows: short SHA, subject, author, relative date.
  - Click a commit → commit-vs-parent. Select two commits → any-two-commits.
  - Infinite-scroll paging via `git log --skip/--max-count`.
- **Middle — Changed files** (for the current comparison):
  - Grouped by folder (tree) with a flat-list toggle.
  - Each row: status glyph (A/M/D/R), path, `+/-` counts; binary files flagged.
- **Right — Diff view:**
  - **Side-by-side by default**; toggle to unified (choice persisted).
  - Syntax highlighting + intra-line (word-level) highlighting + change bars + line numbers.
  - Diff toolbar: split/unified · search-in-diff · next/prev change · word-wrap toggle · show-whitespace toggle · copy · open file in default editor.
  - Header: file path, old→new on rename, status.

**Empty/selection states** for: no comparison selected, no changes in selection, binary/large file.

**Settings** is a small Fluent `ContentDialog`: theme, default diff style, context lines (`-U`), tab size, external-editor command, syntax highlighting on/off.

> **Deliberate template deviation:** Layout A is a custom workspace rather than the template's `NavigationView` multi-page idiom — the right fit for a diff tool. WPF-UI `FluentWindow`, controls, theming, and Fluent icons are still used throughout, and all theme-aware colors use `DynamicResource`.

---

## 6. Architecture & project structure

Pure-WPF flavor of the org's standard desktop template.

**Stack:** .NET 10 (`net10.0-windows10.0.26100`), C# 14, `Nullable` enabled, `TreatWarningsAsErrors`. MVVM via CommunityToolkit.Mvvm. DI via Autofac + IHost (`AutofacServiceProviderFactory`, Module-per-layer). UI via WPF-UI (lepoco). Tests via xUnit v3 + NSubstitute + Shouldly.

```
GitDelta/
├─ src/
│  ├─ GitDelta.UI/      # WPF exe (gitdelta.exe)
│  │                    #   App bootstrap + CLI arg routing + global exception handlers
│  │                    #   FluentWindow shell, 3-pane workspace, start screen
│  │                    #   Views + ViewModels (CommunityToolkit.Mvvm)
│  │                    #   AvalonEdit diff control: background renderer, change-bar
│  │                    #     margin, intra-line colorizer, TextMate tokenizer adapter,
│  │                    #     side-by-side scroll-sync
│  └─ GitDelta.Core/    # classlib (net10.0, WPF-free, unit-testable)
│                       #   IGitReader (CliGitReader) + git output parsers
│                       #   IIntraLineDiffer (DiffPlex)
│                       #   Repo discovery, diff/commit domain models
├─ tests/
│  ├─ GitDelta.Core.Tests/     # parsing, diff, repo discovery, arg routing
│  └─ GitDelta.UI.UnitTests/   # ViewModels (IGitReader mocked)
├─ installer/                  # Inno Setup .iss
├─ Directory.Build.props
├─ Directory.Packages.props    # central package management, pinned versions
├─ .editorconfig
├─ CLAUDE.md
├─ GitDelta.sln
└─ version.json
```

**Layering:** `UI → Core`; Core never references UI. Each layer owns an Autofac `Module`. Services `SingleInstance`; ViewModels `InstancePerDependency`. Constructor injection only.

---

## 7. Git access layer (`GitDelta.Core`)

`IGitReader` shells out to the **git CLI** (no LibGit2Sharp). Process discipline:

- Async byte-stream stdout, drained without deadlock; decode UTF-8 ourselves.
- `-z` (NUL-framed) for all path-bearing output; `-c core.quotepath=false`.
- `--no-optional-locks` (read-only safety); `WorkingDirectory = repo root`.
- `CancellationToken` → `Process.Kill` so long operations stay responsive.

**Concrete read-only command set:**

- **History (paged):**
  `git log --pretty=format:%H%x1f%P%x1f%an%x1f%ae%x1f%aI%x1f%cn%x1f%ce%x1f%cI%x1f%s%x1f%b -z --skip=N --max-count=M`
  (records separated via NUL; fields via `%x1f`; ISO-8601-strict dates; `%P` = parents.)
- **Changed-file list (any pair / commit-vs-parent):**
  `git diff --numstat --name-status -z -M -C <A> <B>`
  (binary → `-\t-` in numstat; rename = NUL-framed two-path record; `-M50%` similarity.)
- **Per-file hunks:**
  `git diff -U<ctx> -M -C <A> <B> -- <path>` (parse `@@` headers + +/-/space lines.)
- **Working tree vs HEAD:**
  `git diff --numstat -z HEAD` for tracked changes **plus** `git status --porcelain=v2 -z` for untracked/renamed/conflicted (so the combined uncommitted view is complete).
- **Root commit:** diff against the empty-tree SHA `4b825dc642cb6eb9a060e54bf8d69288fbee4904`.
- **Merge commit "vs parent":** first parent by default.

**Startup git detection:** `git --version` (require **≥ 2.30** for stable `-z`/`porcelain=v2`). If missing, show an actionable screen linking Git for Windows.

**Parser edge cases to cover:** rename two-path records, binary markers, root/merge/octopus commits, untracked files, non-UTF-8 / quoted paths, CRLF/autocrlf.

---

## 8. Diff computation & rendering pipeline

**Separation of concerns:** git tells us *which lines* changed; **DiffPlex** (in Core, WPF-free) tells us *which words/characters* changed within a modified line-pair. Both are unit-tested independently of WPF.

**Rendering (UI, AvalonEdit, read-only):**
- `IsReadOnly = true`, undo/edit subsystems disabled (lighter).
- Custom `IBackgroundRenderer` paints full-line add/delete/modify backgrounds; a custom margin draws the green/red/orange change bar.
- A `DocumentColorizingTransformer` applies intra-line tints, layered **on top of** the syntax colorizer (syntax transformer added first).
- **Side-by-side** = two AvalonEdit instances; DiffPlex "filler"/imaginary lines keep rows aligned; `TextView.ScrollOffset` mirrored both ways with a feedback guard; **word-wrap disabled** in split mode.
- **Unified** = single AvalonEdit built from the inline diff model.

**Syntax highlighting:** TextMateSharp + TextMateSharp.Grammars (DarkPlus/LightPlus themes) via a hand-written (~150–250 line) TextMate→AvalonEdit tokenizer adapter — no maintained bridge exists, so we own it. Theme swaps on app theme change; editor brushes bound to WPF-UI `DynamicResource` keys; semi-transparent diff backgrounds so Mica/selection still read.

**Performance & safety valves:** all diff/syntax work runs inside per-visible-line transformers (leveraging AvalonEdit virtualization); DiffPlex line classification precomputed once into an indexed lookup. A size/line threshold falls back to monochrome (skip tokenization); binary files show a "binary — no textual diff" placeholder.

**MVP escape hatch:** if the TextMate adapter slips, ship DiffPlex.Wpf's `SideBySideDiffViewer` first and swap in the AvalonEdit view later — the Core diff model is reusable across both.

---

## 9. Settings & persistence

UI preferences persisted to `%APPDATA%\GitDelta\settings.json`: theme, default diff style (side-by-side/unified), context lines, tab size, external-editor command, syntax-on/off, pane sizes, window size/position.

Per product decision: GitDelta does **not** auto-reopen the last repo and keeps **no** recent-repos list.

---

## 10. Error handling & edge cases

- Global handlers: `DispatcherUnhandledException`, `AppDomain.CurrentDomain.UnhandledException`, `TaskScheduler.UnobservedTaskException`; structured logging; no silent/empty catches.
- Friendly states: git not installed; not a git repo; empty repo / first commit; very large or binary diffs; cancelled long operations.
- Never block the UI thread on git I/O (`async`, `ConfigureAwait(false)` in Core, no `.Result`/`.Wait()`).

---

## 11. Packaging & distribution

- **Publish:** framework-dependent single-file `WinExe` — `PublishSingleFile=true`, `IncludeNativeLibrariesForSelfExtract=true`, `EnableCompressionInSingleFile=true`, `SelfContained=false`, **no `PublishTrimmed`** (WPF trimming is SDK-disabled). exe named `gitdelta.exe` (~5–15 MB).
- **Installer:** **Inno Setup**, per-user (`PrivilegesRequired=lowest`, `ChangesEnvironment=yes`):
  - Installs to `%LOCALAPPDATA%\Programs\GitDelta`.
  - Idempotently appends the install dir to `HKCU\Environment\Path` (`REG_EXPAND_SZ`), removed on uninstall. **Never `setx`** (1024-char truncation).
  - Start-menu shortcut + Add/Remove-Programs entry.
  - Detects the **.NET 10 Desktop Runtime**; if missing, runs the bundled `windowsdesktop-runtime` silent installer (or surfaces the download).
- **Unsigned for v1** (SmartScreen "Unknown publisher" warning documented in README). Distributed via GitHub Releases.
- Bare `gitdelta` resolves via PATH (cmd/PowerShell do **not** consult App Paths). New terminals see PATH after the `WM_SETTINGCHANGE` broadcast that `ChangesEnvironment=yes` triggers.

---

## 12. Testing strategy

- **Core (xUnit v3 + Shouldly):** `IGitReader` parsing against canned git output (numstat, name-status, log records, status porcelain v2) covering renames, binary, unicode/quoted paths, root/merge commits; DiffPlex intra-line results; repo discovery (walk-up to `.git`); CLI arg-routing logic.
- **UI (NSubstitute):** ViewModel behavior (mode selection from history, file selection, diff-style/word-wrap/whitespace toggles, persistence) with `IGitReader` mocked. No tests against a live AvalonEdit/WebView.
- Build gate: `dotnet build` with zero warnings (`TreatWarningsAsErrors`); `dotnet test tests/`.

---

## 13. Dependencies & pinned versions

Pinned in `Directory.Packages.props` (central package management); verify exact current versions at integration time.

| Concern | Package / tool | Version (as of 2026-05) | License |
|---|---|---|---|
| MVVM | CommunityToolkit.Mvvm | latest | MIT |
| DI | Autofac (+ `Autofac.Extensions.DependencyInjection`) | latest | MIT |
| UI / Fluent | WPF-UI (lepoco) | latest | MIT |
| Code editor | AvalonEdit | 6.3.1.120 | MIT |
| Diff (line + word/char) | DiffPlex | 1.9.x core | Apache-2.0 |
| Syntax grammars/themes | TextMateSharp + .Grammars | 2.0.3 | MIT |
| Tests | xUnit v3, NSubstitute, Shouldly | latest | — |
| External | git CLI (Git for Windows) | ≥ 2.30 | GPLv2 (separate process) |
| Runtime | .NET 10 Desktop Runtime | 10.x | — |
| Installer | Inno Setup | 6.x | — |

> Licensing note: invoking git as a separate process is mere aggregation and does **not** impose GPL on GitDelta. AvalonEdit (MIT), DiffPlex (Apache-2.0), TextMateSharp (MIT) are all safe to link.

---

## 14. Out of scope for v1 (future candidates)

Branch-vs-branch · staged/unstaged tri-state · recent-repos / multi-repo tabs · code-signing · drag-and-drop open · commit search/filter · blame / file history · LibGit2Sharp "no-git" fallback · minimap / code folding.

---

## 15. Key risks & decisions log

- **Risk — TextMate→AvalonEdit adapter:** no maintained WPF bridge; we own ~150–250 lines. Mitigation: well-precedented pattern; MVP escape hatch (DiffPlex.Wpf) if it slips.
- **Risk — AvalonEdit ships net6.0-windows assemblies:** load via forward-compat on net10; with `TreatWarningsAsErrors` may need scoped nullable/obsolete suppressions.
- **Decision — git CLI over LibGit2Sharp:** lighter, correct, LFS/submodule support, avoids LibGit2Sharp's .NET 10 marshalling/GC risk (dotnet/runtime #104872). git required on PATH.
- **Decision — Pure WPF over Hybrid/Monaco:** best serves lightweight + native + read-only and the `gitdelta .` cold-start fast-path, at the cost of more custom rendering code.
- **Decision — Windows 11 only**, framework-dependent build, unsigned v1.
