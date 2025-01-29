[简体中文版](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README.md) | ENGLISH VERSION
# LineLauncherCs

## Description
Line Launcher（or LMC）is a Minecraft Launcher. This repository is its C# version. You can find the link on [Official Website](https://line.icecreamteam.win/index-en.html) If you want to see Visual Basic version.

## Code
All codes in this repository are developed by [Huangyu](https://github.com/tmdakm)(Not related with YellowFish which developed another launcher).    
It can launch\download\login Minecraft.    
Uses .Net Framework v4.7.2 & WPF to develop GUI, GUI library is [iNKORE.UI.WPF.Modern](https://github.com/iNKORE-NET/UI.WPF.Modern/).    
When release, it use Costura.Fody to make only one exe file.    
And, LMC C# is using a special file ``.line`` to save LMC Basic information(Log Number\Latest Launched Version\Player's Account and others). This is its format:    
```
|category|_start
|key|:|value|
|category|_end
```
It likes InI file:  
```
[category]
key=value

[anothercategory]
anotherkey=anothervalue
```

## Help US
Welcome to submit Pull Requests\Issues or send email to <line@huangyu.win> if you want to let LMC better.  

## License
Please comply with the content of the LICENSE and put this repository's link if you want to fork this repository or use LMC's codes.

## Debug arguments
| Console arguments         | Description |
| ------------------ | ----------- |
| -debug               | Enable debug mode. It will log more logs.                                 |
| -start <version>   | Start version when start launcher. If version does not exist, programm will exit as code 2.  |
| -reset             | Reset all configuration of LMC, but device-code will not be change.                          |
| -reacc             | Delete all saved Microsoft/Another/Offline accounts.                                         |

## Library Copyright Statements
(Translated by ChatGPT, The Chinese version shall prevail)
### SegueFluentIcons
Developed by Microsoft, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license.

### Costura.Fody & Fody
- [Costura](https://github.com/Fody/Costura)
- [Fody](https://github.com/Fody/Fody)

Developed by Simon Cropp and contributors, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license.

### iNKORE.UI.WPF & iNKORE.UI.WPF.Modern
[iNKORE.UI.WPF.Modern](https://github.com/iNKORE-NET/UI.WPF.Modern/)

Developed by NotYoojun, contributors, and contributors to the base project, open-sourced under the [LGPL v2.1](https://www.gnu.org/licenses/old-licenses/lgpl-2.1.en.html) license.

### Newtonsoft.Json
Developed by James Newton-King and contributors, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license.

### Shared.Common
Developed by tmacharia, open-sourced under the [MIT](https://licenses.nuget.org/MIT) license.

### Others
Includes the .NET Framework, .NET, .NET Desktop, Windows Presentation Foundation, and some (or all) content under the `System` namespace, developed by Microsoft.
- Portions are open-sourced under the [MIT](https://licenses.nuget.org/MIT) license.
- Others are licensed under the [.NET Library License](https://dotnet.microsoft.com/en-us/dotnet_library_license.htm).

Most details can be found in the [.NET Licensing Information](https://github.com/dotnet/core/blob/main/license-information.md).
