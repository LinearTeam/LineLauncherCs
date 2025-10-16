[中文 (简体)](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README.md) | English (US)

## Line Launcher
**LineLauncher** is a cross-platform Fluent-style Minecraft launcher developed by [LinearTeam](https://github.com/LinearTeam), also abbreviated as LMC.  
This is its C# version repository. For the Visual Basic version, please refer to our [official website](https://line.icecreamteam.win).

The term "LMC" mentioned below refers to the C# version repository.

## Introduction
### History
The current `main` branch you are viewing is developed based on [Avalonia](https://docs.avaloniaui.net/). This UI framework differs from WPF, enabling native .NET cross-platform capabilities while maintaining UI design patterns similar to WPF.

LMC previously had a WPF version located in the `wpf` branch. Due to WPF's lack of cross-platform support and other limitations, we transitioned to Avalonia for cross-platform development.

### Roadmap
- ~~Project reconstruction~~
- ~~Basic UI~~
- ~~Java management~~
- Help Center
- Version management
- Account management
- OOBE
- Game downloads
- Game launching
- Mod management
- Extension support
- ...

### Project Structure (AI generated)
```
LineLauncherCs/
├── LMC/                    # Basic module
│   ├── Basic/              # Basic features
│   │   ├── Configs/        # Configuration files
│   │   ├── Logging/        # Logging functionality
│   │   ├── LineFileParser.cs  # Line file parser
│   │   └── TaskCallbackInfo.cs  # Task callback information
│   ├── Current.cs          # Current state
│   ├── LMC.csproj          # Project file
│   └── LifeCycle/          # Lifecycle
│       └── Startup.cs      # Startup logic
├── LMCCore/                # Core module
│   ├── Account/            # Account management
│   ├── Java/               # Java management
│   ├── Utils/              # Utility classes
│   └── LMCCore.csproj      # Project file
├── LMCCore.Test/           # Test module (AI generated)
│   ├── Account/            # Account tests
│   ├── Java/               # Java tests
│   ├── Utils/              # Utility tests
│   └── LMCCore.Test.csproj # Test project file
└── LMCUI/                  # UI module
    ├── Assets/             # Resource files
    ├── Behaviors/          # Behaviors
    ├── Controls/           # Controls
    ├── I18n/               # Internationalization
    ├── Languages/          # Language files
    ├── Pages/              # Pages
    ├── Utils/              # UI utilities
    └── LMCUI.csproj        # Project file
```

### Dependency Library Copyright Notice
#### System packages
And the .NET Framework, .NET, .NET Desktop, and some (or all) content under the System namespace developed by Microsoft,
Some of which are open-sourced under the [MIT](https://licenses.nuget.org/MIT) license,
Some are open-sourced under the [.NET Library License](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm)
Most can be found in the [.NET License Information](https://github.com/dotnet/core/blob/main/license-information.md)

#### Avalonia packages
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) 
  - [Avalonia.Desktop](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Themes.Fluent](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Fonts.Inter](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Diagnostics](https://github.com/AvaloniaUI/Avalonia)

  Developed by the AvaloniaUI team, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license

#### FluentAvalonia packages
- [FluentAvaloniaUI](https://github.com/avaloniaui/FluentAvalonia) 

  Developed by the AvaloniaUI team, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license

#### Other packages
- [NLog](https://github.com/NLog/NLog) 

  Developed by the NLog team, open-sourced under the [BSD 3-Clause](https://licenses.nuget.org/BSD-3-Clause) license

- [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) 

  Developed by Microsoft, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license

- [xunit](https://github.com/xunit/xunit) 

  Developed by the xUnit team, open-sourced under the [Apache-2.0](https://licenses.nuget.org/Apache-2.0) license

- [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) 

  Developed by the xUnit team, open-sourced under the [Apache-2.0](https://licenses.nuget.org/Apache-2.0) license

- [coverlet.collector]

  Developed by the Coverlet team, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license

- [Moq](https://github.com/moq/moq) 

  Developed by the Moq team, open-sourced under the [BSD 3-Clause](https://licenses.nuget.org/BSD-3-Clause) license