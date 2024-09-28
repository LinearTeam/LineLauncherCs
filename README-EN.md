[简体中文版](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README.md) | ENGLISH VERSION
# LineLauncherCs

## Description
Line Launcher（or LMC）is a Minecraft Launcher. This repository is its C# version. You can find the link on [Official Website](https://line.icecreamteam.win/index-en.html) If you want to see Python version.

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
Please comply with the content of the LICENSE(ENGLISH VERSION:LICENSE-EN) and put this repository's link if you want to fork this repository or use LMC's codes.

## Debug arguments
(It will enable in beta edition)
| Console arguments         | Description |
| ------------------ | ----------- |
| -log               | Enable log output. It will log more logs and log to console.                                 |
| -logF <Path>       | Set log file.                                                                                |
| -start <version>   | Start version when start launcher. If version does not exist, programm will exit as code 2.  |
| -reset             | Reset all configuration of LMC, but device-code will not be change.                          |
| -reacc             | Delete all saved Microsoft/Another/Offline accounts.                                         |
