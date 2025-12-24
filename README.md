中文 (简体) | [English (US)](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README-EN.md)

## Line Launcher
**LineLauncher**是一款由[LinearTeam](https://github.com/LinearTeam)开发的跨平台Fluent风格 Minecraft启动器，它也可以被简写为LMC。  
这是它的C#版本仓库，关于Visual Basic版本，请参阅我们的[官网](https://line.icecreamteam.win)。

下文所述的LMC均指代C#版本仓库。

## 简介
### 历史
您目前所在的`main`分支是基于[Avalonia](https://docs.avaloniaui.net/)开发的，此UI框架不同于WPF，它支持在.NET原生的跨平台能力上实现与WPF相似的UI设计。

LMC曾有一个WPF版本，位于`wpf`分支下。由于WPF的无法跨平台等因素，我们转向Avalonia并进行跨平台开发。

### 路线图
 - ~~重建项目~~
 - ~~基础UI~~
 - ~~Java管理~~
 - 帮助库
 - 版本管理
 - 账号管理
 - OOBE
 - 下载游戏
 - 启动游戏
 - 模组管理
 - 扩展支持
 - ...

### 项目结构 (AI 生成)
```
LineLauncherCs/
├── LMC/                    # 基础模块
│   ├── Basic/              # 基础功能
│   │   ├── Configs/        # 配置文件
│   │   ├── LineFileParser.cs  # .line 文件解析器
│   │   ├── Logging/        # 日志功能
│   │   └── TaskCallbackInfo.cs  # 任务回调信息
│   ├── Current.cs          # 当前状态
│   ├── LMC.csproj          # 项目文件
│   └── LifeCycle/          # 生命周期
│       └── Startup.cs      # 启动逻辑
├── LMCCore/                # 核心模块
│   ├── Account/            # 账号管理
│   ├── Java/               # Java管理
│   ├── Utils/              # 工具类
│   └── LMCCore.csproj      # 项目文件
└── LMCUI/                  # UI模块
    ├── Assets/             # 资源文件
    ├── Behaviors/          # 行为
    ├── Controls/           # 控件
    ├── I18n/               # 国际化
    ├── Languages/          # 语言文件
    ├── Pages/              # 页面
    ├── Utils/              # UI工具
    └── LMCUI.csproj        # 项目文件
```

### 依赖库版权声明
#### System包
以及由 Microsoft 开发的 .NET Framework、.NET、.NET Desktop、部分（或全部）位于 System 命名空间下的内容，
其中部分使用[MIT](https://licenses.nuget.org/MIT)协议开源，
部分使用[.NET库许可证](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm)开源
大部分可以在[.NET协议说明](https://github.com/dotnet/core/blob/main/license-information.md)中找到

#### Avalonia包
- [Avalonia](https://github.com/AvaloniaUI/Avalonia) 
  - [Avalonia.Desktop](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Themes.Fluent](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Fonts.Inter](https://github.com/AvaloniaUI/Avalonia)
  - [Avalonia.Diagnostics](https://github.com/AvaloniaUI/Avalonia)

  由 AvaloniaUI 团队开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

#### FluentAvalonia包
- [FluentAvaloniaUI](https://github.com/amwx/FluentAvalonia) 

  由 amwx 开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

#### 其他包
- [NLog](https://github.com/NLog/NLog) 

  由 NLog 团队开发，使用[BSD 3-Clause](https://licenses.nuget.org/BSD-3-Clause)协议开源

- [Microsoft.NET.Test.Sdk](https://github.com/microsoft/vstest) 

  由 Microsoft 开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

- [xunit](https://github.com/xunit/xunit) 

  由 xUnit 团队开发，使用[Apache-2.0](https://licenses.nuget.org/Apache-2.0)协议开源

- [xunit.runner.visualstudio](https://github.com/xunit/visualstudio.xunit) 

  由 xUnit 团队开发，使用[Apache-2.0](https://licenses.nuget.org/Apache-2.0)协议开源

- [coverlet.collector](https://github.com/coverlet-coverage/coverlet) 

  由 Coverlet 团队开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

- [Moq](https://github.com/moq/moq) 

  由 Moq 团队开发，使用[BSD 3-Clause](https://licenses.nuget.org/BSD-3-Clause)协议开源