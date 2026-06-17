<p align="center">
  <img src="cover.png" alt="DotNetFM Banner" width="512" />
</p>

<h1 align="center">DotNetFM</h1>

<p align="center">
  <strong>A modern, Linux-inspired file manager for Windows — built from scratch with C# and WPF.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET 8" />
  <img src="https://img.shields.io/badge/C%23-12.0-239120?style=for-the-badge&logo=csharp&logoColor=white" alt="C#" />
  <img src="https://img.shields.io/badge/WPF-0A0A0A?style=for-the-badge&logo=windows&logoColor=white" alt="WPF" />
  <img src="https://img.shields.io/badge/Status-Alpha-orange?style=for-the-badge" alt="Status: Alpha" />
  <img src="https://img.shields.io/badge/Version-0.0.1--alpha-blue?style=for-the-badge" alt="Version" />
  <img src="https://img.shields.io/github/license/rilex037/dot-net-fm?style=for-the-badge" alt="License" />
</p>

---

## What is DotNetFM?

DotNetFM is a lightweight, fast, and visually polished file manager for Windows. It draws deep inspiration from **[GNOME Nautilus](https://wiki.gnome.org/Apps/Files)** and the broader Linux desktop ecosystem, bringing that clean, purposeful design language to the Windows platform.

Every pixel, every interaction, and every component is **built from the ground up** — no bloated UI frameworks, no third-party file manager shells. Just pure **C#**, **WPF**, and a lot of Win32 interop where Windows demands it.

## Why?

Most Windows file managers are either stuck in the Windows 7 era or packed with features nobody asked for. DotNetFM takes a different approach: a **minimal, focused file manager** that feels right at home on a modern desktop, borrowing the best ideas from the Linux world while embracing native Windows capabilities under the hood.

## DotNetFM Architecture

## System Overview

WPF desktop file manager with module-based backend system.

```
┌─────────────────────────────────────────────┐
│                  DOTNETFM                   │
│  WPF + .NET 8 + Modular Architecture        │
└─────────────────────────────────────────────┘
```

## Layered Design

```
┌─────────────────────────────────────────┐
│         UI Layer (WPF)                  │
│  MainWindow + Controls                  │
└──────────────┬──────────────────────────┘
               │
┌──────────────▼───────────────────────────┐
│         Service Layer                    │
│  TabManager / FileInteraction / etc.     │
└──────────────┬───────────────────────────┘
               │
┌──────────────▼───────────────────────────┐
│    Core Abstractions (Interfaces)        │
│  IModule / IFileProvider / IFileOps      │
└──────────────┬───────────────────────────┘
               │
┌──────────────▼───────────────────────────┐
│         Module Implementations           │
│  WindowsModule / (future modules)        │
└──────────────────────────────────────────┘
```

## Core Abstractions

```
IModule
├── FileProvider    → IFileProvider
├── FileOperations  → IFileOperations  
├── IconProvider    → IIconProvider
├── ContextMenu     → IContextMenuProvider (optional)
└── DirectoryWatcher → IDirectoryWatcher
```

## Module Discovery

```
App startup
  └── ModuleRegistry.ScanAndRegisterAll()
        ├── Scan for DotNetFM.Module.*.dll
        ├── Load assemblies
        ├── Find IModule implementations
        └── Register by UriPrefix
              ("windows", "shell") → WindowsModule
```

## Data Flow

```
User Action
  └── FileGridView
        └── FileInteractionService
              └── TabManager.DispatchActive(action)
                    └── TabReducer applies action
                          └── IModule.FileProvider.GetContents()
                                └── Returns FolderItems
                                      └── TabStore.StateChanged
                                            └── MainWindow updates UI
```

## Tab State Management

```
TabManager
├── ObservableCollection<TabStore>
├── ActiveTab
└── AddTab / SetActiveTab / DispatchActive

TabStore
├── State (TabStateRecord)
│   ├── ActivePath
│   ├── CanGoBack / CanGoForward
│   ├── IconSize
│   └── StatusText
├── Folders (ObservableCollection<FolderItem>)
└── StateChanged event
```

## UI Components

```
MainWindow
├── TitleBar
├── SidebarPanel (module-contributed sections)
├── FileViewContainer → FileGridView
├── NavigationToolbar (Back/Forward/Up/Address)
├── TabStripBuilder
└── StatusBar (zoom + status)
```

## Key Patterns

- **Module auto-discovery** via assembly scan - zero-config extensibility
- **Thin MainWindow** - logic lives in services
- **TabStore + TabReducer** - unidirectional state flow
- **Per-module sidebar sections** - each backend contributes its own locations
- **UriPrefix routing** - maps paths to modules

## Tech Stack

- **Language:** C# 12
- **UI Framework:** WPF (.NET 8)
- **Icon Rendering:** [SharpVectors](https://github.com/ElinamLLC/SharpVectors) for SVG support
- **Native Interop:** Win32 API via P/Invoke for shell menus, icons, and monitor info

## License

This project is licensed under the [MIT License](LICENSE).

---