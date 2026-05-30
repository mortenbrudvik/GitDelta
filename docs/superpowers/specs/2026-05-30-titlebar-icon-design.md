# App icon in the title bar — Design

**Date:** 2026-05-30
**Status:** Approved
**Scope:** `src/GitDelta.UI` only

## Goal

Show the existing `gitdelta.ico` as the glyph at the top-left of the main window's
title bar, and ensure the running app's taskbar button and Alt+Tab switcher use the
same icon.

## Background

- The icon asset already exists at `src/GitDelta.UI/Assets/gitdelta.ico` and is wired
  as `<ApplicationIcon>` in `GitDelta.UI.csproj`, so the **.exe file icon** (Explorer,
  taskbar pinning) already uses it.
- `Views/MainWindow.xaml` is a `ui:FluentWindow` (WPF-UI 4.3.0) with
  `ExtendsContentIntoTitleBar="True"` and a custom `ui:TitleBar`. Because the content
  extends into the title bar, the OS no longer draws the native non-client title bar
  (which is what would normally show the window icon). Neither `TitleBar.Icon` nor
  `Window.Icon` is currently set, so the title bar shows no glyph.

## Changes

All changes are in the `GitDelta.UI` project. No Core changes, no ViewModel logic.

### 1. Expose the icon as a WPF pack resource — `GitDelta.UI.csproj`

`<ApplicationIcon>` only embeds the icon into the .exe's Win32 resources; it does **not**
make it reachable from XAML via a pack URI. Add a `Resource` include so
`pack://application:,,,/Assets/gitdelta.ico` resolves at runtime:

```xml
<ItemGroup>
  <Resource Include="Assets\gitdelta.ico" />
</ItemGroup>
```

`<ApplicationIcon>` and `<Resource>` referencing the same file is fine — they serve
different purposes (Win32 exe icon vs. WPF embedded resource) and do not conflict.

### 2. Title-bar glyph — `Views/MainWindow.xaml`

In WPF-UI 4.x, `TitleBar.Icon` is an `IconElement`, so it is set with an `ImageIcon`
(not a raw `ImageSource`):

```xml
<ui:TitleBar x:Name="TitleBar" Grid.Row="0" Title="GitDelta">
  <ui:TitleBar.Icon>
    <ui:ImageIcon Source="pack://application:,,,/Assets/gitdelta.ico" />
  </ui:TitleBar.Icon>
  <!-- existing TitleBar.Header unchanged -->
</ui:TitleBar>
```

### 3. Window / Alt+Tab icon — `Views/MainWindow.xaml`

Add WPF's `Window.Icon` (an `ImageSource`) to the `ui:FluentWindow` root element so the
taskbar button and Alt+Tab switcher use the icon consistently rather than relying on the
embedded-exe fallback:

```xml
<ui:FluentWindow
    x:Class="GitDelta.UI.Views.MainWindow"
    ...
    Icon="pack://application:,,,/Assets/gitdelta.ico"
    ...>
```

## Why this works

`ExtendsContentIntoTitleBar="True"` suppresses the OS-drawn title bar, so the
`ui:TitleBar` control must be told to render the icon itself (change 2). `Window.Icon`
(change 3) feeds the taskbar button and Alt+Tab directly.

## Out of scope (YAGNI)

- No new icon artwork — reuse the existing `gitdelta.ico`.
- No per-view icons; `StartView` and dialogs are unchanged.
- No settings toggle for showing/hiding the icon.

## Testing & verification

There is no ViewModel logic to unit-test, and per CLAUDE.md, UI tests are
ViewModel-only (no tests against live WPF controls). Verification is:

1. `dotnet build GitDelta.sln` — must stay zero-warning (`TreatWarningsAsErrors=true`).
2. `dotnet run --project src/GitDelta.UI/` — visually confirm the glyph appears at the
   top-left of the title bar and in the Alt+Tab switcher.

## Build gate

`dotnet build` zero warnings and `dotnet test tests/` passing before commit.
