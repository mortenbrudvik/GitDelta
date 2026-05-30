# Title-Bar App Icon Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Display the existing `gitdelta.ico` as the title-bar glyph of the main window and as the running app's taskbar/Alt+Tab icon.

**Architecture:** The window is a WPF-UI 4.3.0 `ui:FluentWindow` with `ExtendsContentIntoTitleBar="True"`, so the OS does not draw the native title bar. We expose the existing icon as a WPF pack `Resource`, render it via `ui:TitleBar.Icon` (an `IconElement`), and set WPF's `Window.Icon` for the taskbar/Alt+Tab. No Core or ViewModel changes.

**Tech Stack:** WPF (.NET 10), WPF-UI 4.3.0, MSBuild SDK-style csproj.

**Verification note:** This change is pure XAML/csproj wiring with no ViewModel logic. Per `CLAUDE.md`, UI tests are ViewModel-only with `IGitReader` mocked — there is nothing meaningful to unit-test here. The verification gate is a zero-warning build (`TreatWarningsAsErrors=true`) plus a one-time visual confirmation via `dotnet run`. The csproj `Resource` and the two XAML edits are interdependent (the XAML pack URI resolves the `Resource` at runtime), so they land in a single commit to avoid a runtime-broken intermediate state.

---

### Task 1: Wire `gitdelta.ico` into the title bar and window

**Files:**
- Modify: `src/GitDelta.UI/GitDelta.UI.csproj` (add a `Resource` ItemGroup)
- Modify: `src/GitDelta.UI/Views/MainWindow.xaml:1-26` (window `Icon` + `TitleBar.Icon`)

- [ ] **Step 1: Capture the baseline build state**

Confirms the tree builds clean before any edit, so a later warning/error is unambiguously ours.

Run: `dotnet build GitDelta.sln`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 2: Expose the icon as a WPF pack resource**

In `src/GitDelta.UI/GitDelta.UI.csproj`, add a new `ItemGroup` (place it next to the existing `ItemGroup` blocks, e.g. after the `ProjectReference` ItemGroup ending on line 31). `<ApplicationIcon>` only embeds the Win32 exe icon; a `Resource` is required for the `pack://` URI to resolve at runtime.

```xml
  <ItemGroup>
    <!-- Same .ico as <ApplicationIcon> above, also embedded as a WPF resource so the
         title bar / Window.Icon pack URI resolves at runtime. -->
    <Resource Include="Assets\gitdelta.ico" />
  </ItemGroup>
```

- [ ] **Step 3: Set `Window.Icon` on the FluentWindow root**

In `src/GitDelta.UI/Views/MainWindow.xaml`, add the `Icon` attribute to the `ui:FluentWindow` opening tag. Insert it alphabetically among the existing attributes — between `ExtendsContentIntoTitleBar="True"` (line 12) and `WindowBackdropType="Mica"` (line 13):

```xml
    ExtendsContentIntoTitleBar="True"
    Icon="pack://application:,,,/Assets/gitdelta.ico"
    WindowBackdropType="Mica"
```

- [ ] **Step 4: Set the title-bar glyph via `TitleBar.Icon`**

In `src/GitDelta.UI/Views/MainWindow.xaml`, the `ui:TitleBar` currently opens on lines 23-26 and its first child is `<ui:TitleBar.Header>` (line 27). Insert a `ui:TitleBar.Icon` property element immediately after the `ui:TitleBar` opening tag and before `<ui:TitleBar.Header>`. In WPF-UI 4.x `TitleBar.Icon` is an `IconElement`, so it must be an `ImageIcon` (not a raw `ImageSource`).

Change this:

```xml
    <ui:TitleBar
        x:Name="TitleBar"
        Grid.Row="0"
        Title="GitDelta">
      <ui:TitleBar.Header>
```

to this:

```xml
    <ui:TitleBar
        x:Name="TitleBar"
        Grid.Row="0"
        Title="GitDelta">
      <ui:TitleBar.Icon>
        <ui:ImageIcon Source="pack://application:,,,/Assets/gitdelta.ico" />
      </ui:TitleBar.Icon>
      <ui:TitleBar.Header>
```

- [ ] **Step 5: Build and verify zero warnings**

Run: `dotnet build GitDelta.sln`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`. A pack-URI typo or wrong `Icon` element type surfaces here as a XAML/markup-compile error.

- [ ] **Step 6: Run the test suite (regression guard)**

Run: `dotnet test tests/`
Expected: All tests pass. (No new tests; this confirms the XAML change didn't break the markup-compiled assembly the UI tests load.)

- [ ] **Step 7: Visual confirmation**

Run: `dotnet run --project src/GitDelta.UI/`
Expected:
- The `gitdelta` glyph appears at the **top-left of the window title bar**, left of the "GitDelta" title text.
- The **Alt+Tab** switcher and the **taskbar button** for the running app show the `gitdelta` icon.

Close the app when confirmed.

- [ ] **Step 8: Commit**

```bash
git add src/GitDelta.UI/GitDelta.UI.csproj src/GitDelta.UI/Views/MainWindow.xaml
git commit -m "feat(ui): show app icon in the title bar and Alt+Tab"
```

---

## Self-Review

**Spec coverage:**
- Spec change 1 (Resource include) → Task 1, Step 2. ✓
- Spec change 2 (`TitleBar.Icon` glyph) → Task 1, Step 4. ✓
- Spec change 3 (`Window.Icon`) → Task 1, Step 3. ✓
- Spec verification (zero-warning build + visual check) → Steps 5-7. ✓
- Spec build gate (`dotnet test tests/` passing) → Step 6. ✓

**Placeholder scan:** No TBD/TODO/"handle edge cases" — every step shows exact file, exact XAML/XML, and exact command. ✓

**Type consistency:** The same pack URI `pack://application:,,,/Assets/gitdelta.ico` is used for both `Window.Icon` (ImageSource) and `ui:ImageIcon.Source`; both accept this URI. `TitleBar.Icon` is correctly wrapped in `ui:ImageIcon` (IconElement), not assigned a raw ImageSource. ✓
