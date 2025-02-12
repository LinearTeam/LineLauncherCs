简体中文版 | [ENGLISH VERSION](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README-EN.md)
# <img src="/ico.ico" alt="Logo" width="25" height="25"> LineLauncherCs
![](https://img.shields.io/github/license/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/last-commit/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/repo-size/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/stars/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/contributors/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/commit-activity/y/LinearTeam/LineLauncherCs)
![](https://img.shields.io/github/v/release/LinearTeam/LineLauncherCs?include_prereleases)
![](https://img.shields.io/github/downloads/LinearTeam/LineLauncherCs/total)

## 简介
Line Launcher（亦称LMC）是一个Minecraft启动器，这是它的C#版本，如果想查看Visual Basic版本，可以在[官网](https://line.icecreamteam.win)上寻找链接。

## 代码
本仓库所有代码由[皇鱼](https://github.com/tmdakm)开发。(和另一个启动器的黄鱼无关!)  
将实现Minecraft的启动、下载、登录等功能。  
使用.Net Framework v4.7.2 & WPF制作UI，UI库是[iNKORE.UI.WPF.Modern](https://github.com/iNKORE-NET/UI.WPF.Modern/)。  
在打包时，会安装包Costura.Fody来实现单文件编译。  
除此之外，LMC C#中包含一种特殊的文件格式``.line``，用于存储LMC的基本信息（log文件、上次启动的版本、玩家账号等）。这是它的基本语法：  
```
|分类名|_start
|键|:|值|
|分类名|_end
``` 
可以参考一种ini文件：  
```
[category]
key=value

[anothercategory]
anotherkey=anothervalue
```

## 帮助我们
如果你想让LMC变得更好，欢迎提交Pull Request、Issue或者发邮件给<line@huangyu.win>。

## 协议
如果你要fork本仓库或者用里面的代码，请遵守LICENSE中的内容。并且明确注明使用了LMC的代码并附上仓库链接。


## 调试参数
| 命令行参数         | 作用 |
| ------------------ | ----------- |
| -debug               | 启用调试模式。这将输出更详细的日志。        |
| -start <版本名称>   | 启动指定版本。如果不存在，则以返回码2退出程序。                  |
| -reset             | 删除所有LMC日志及设置，但设备码不变。                           |
| -reacc             | 删除所有存储的微软/第三方/离线账户。                            |


## 引用库的版权声明
### SegueFluentIcons
由 Microsoft 开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

### Costura.Fody & Fody
 - [Costura](https://github.com/Fody/Costura)
 - [Fody](https://github.com/Fody/Fody)

由 Simon Cropp 及其贡献者开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

### iNKORE.UI.WPF & iNKORE.UI.WPF.Modern
[iNKORE.UI.WPF.Modern](https://github.com/iNKORE-NET/UI.WPF.Modern/)

均由 NotYoojun 及其贡献者、基项目贡献者开发，使用[LGPL v2.1](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.en.html)协议开源

### Newtonsoft.Json
由 James Newton-King 及其贡献者开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

### Shared.Common
由 tmacharia 开发，使用[MIT](https://licenses.nuget.org/MIT)协议开源

### 其他
以及由 Microsoft 开发的 .NET Framework、.NET、.NET Desktop、Windows Presentation Foundation、部分（或全部）位于 System 命名空间下的内容，
其中部分使用[MIT](https://licenses.nuget.org/MIT)协议开源，
部分使用[.NET库许可证](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm)开源

大部分可以在[.NET协议说明](https://github.com/dotnet/core/blob/main/license-information.md)中找到
