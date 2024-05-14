简体中文版 | [ENGLISH VERSION](https://github.com/IceCreamTeamICT/LineLauncherCs/blob/main/README-EN.md)
# LineLauncherCs

## 简介
Line Launcher（亦称LMC）是一个Minecraft启动器，这是它的C#版本，如果想查看Python版本，可以在[官网](https://line.icecreamteam.win)上寻找链接。

## 代码
本仓库所有代码由[皇鱼](https://github.com/tmdakm)开发(和那位黄鱼无关!)开发。  
将实现Minecraft的启动、下载、登录等功能。  
使用.Net Framework v4.7.2 & WPF制作UI，UI库是[WPF-UI](https://github.com/lepoco/wpfui)。  
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
(将会在Beta版中启用)
| 命令行参数         | 作用 |
| ------------------ | ----------- |
| -log               | 启用日志输出。这将输出更详细的日志并将日志输出到控制台中。        |
| -logF <路径>       | 指定日志路径。                                                 |
| -start <版本名称>   | 启动指定版本。如果不存在，则以返回码2退出程序。                  |
| -reset             | 删除所有LMC日志及设置，但设备码不变。                           |
| -reacc             | 删除所有存储的微软/第三方/离线账户。                            |
