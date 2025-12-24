*For English version, please refer to [CONTRIBUTING-EN.md](./CONTRIBUTING-EN.md).*
# 贡献指南

其实这篇指南更是给我自己看的，同时欢迎任何形式的贡献，哪怕你不会写代码，遇到问题时开个issue、pr，都是对本项目的支持。  

## 本地化 | Localization
请前往 [Crowdin](https://zh.crowdin.com/project/linelaunchercs) 获取进一步信息。  
*Please refer to [Crowdin](https://crowdin.com/project/linelaunchercs) for more information.*

如果你想让项目支持一种新语言，请于**GitHub的**Issue中提出，将在讨论后决定是否加入。  
*Open an issue **in GitHub** if you want to add a new language, and it will be considered after discussion.*

如果你已经有某个新语言翻译后的语言文件，我们将尽可能使其加入以不让译者的努力白费。  
*We will try our best to include any new language files you have translated to avoid wasting the effort of translators.*

## 开发环境设置

1. 确保已安装 .NET (8.0, 可能会更新)
2. 克隆仓库到本地
3. 使用 IDE 打开 `LineLauncherCs.sln` 解决方案
4. 恢复 NuGet 包依赖
5. 构建解决方案以确保一切正常

## 编码风格

### 通用规则

```text
PascalCase: 开头大写，后面每个单词开头用大写
camelCase: 开头小写，后面每个单词开头用大写
```

1. **命名约定**
   - 类名、方法名、属性名使用 PascalCase
   - 私有字段使用 camelCase 并以 `_` 前缀开头
   - 常量使用 PascalCase
   - 静态私有字段使用 `s_` 前缀
   - 将 `protected` 的命名规则与 `private` 保持一致
   - 接口名称以大写字母 `I` 开头，后跟 Pascal *~~然而本项目截止写这里的时候只有一个`interface`~~*

2. **代码格式**
   - 使用 4 个空格进行缩进 (建议修改 IDE 设置)
   - 花括号 `{}` 另起一行
   - 每行代码长度不超过 120 个字符
   - 使用空行分隔逻辑块

3. **注释**
   - 最好进行注释，但无需过于详细，也可以是一点小吐槽，中文英文皆可，要求能看懂。
   - 尽量减少 XML 文档注释，除非有特殊情况要说明

4. **异常处理**
   - 对可能抛出异常的代码进行适当的异常捕获
   - 使用具体的异常类型而非通用异常 (用了其实也没什么关系。)
   - 捕获异常后应提供有意义的错误信息到日志中

### 项目结构约定

1. **命名空间**
   - 遵循项目结构命名，例如：`LMC.Basic.Configs`
   - 不要创太深的命名空间，以`LMCUI.Pages.AccountPage.AddAccount.Offline`为例，不应将`GameIdWarnStep`放到更进一级的命名空间。在部分需要特别多操作/页面的UI命名空间享受豁免，如较深的设置页面，但应尽量避免此情况。
   
2. **文件组织**
   - 每个类应放在单独的文件中，除非该类特别小、功能重复、是模型类（`record`、`enum`...）
   - 文件名应与类名保持一致
   - 相关功能应放在同一目录下

3. **依赖管理**
   - 尽量避免循环依赖
   - ~~尝试使用依赖注入（其实不用，因为我也不用）~~

### UI 编码规范

1. **XAML 文件**
   - 使用 `axaml` 文件扩展名
   - ~~为可重用 UI 元素创建自定义控件（骗你的，我也不创建）~~
   - 无需遵循 MVVM 等模式，但是不要在 `LMCUI` 项目中编写业务代码 *（你可以在里面调用业务代码，注意分离UI线程和任务线程）*，而是使用 `LMCCore`、`LMC` 中的业务代码。

2. **国际化**
   - 所有用户可见的字符串应通过 `I18nManager` 获取，预料中可能由用户触发的的错误信息同样，部分不会由用户触发的错误信息除外
   - 新增的字符串需添加到 `zh-CN.xml` 语言文件中，**不要在代码中添加其他语言！** 在版本发布时会通过 [Crowdin](https://zh.crowdin.com/project/linelaunchercs) 管理翻译，如果你真的想为你的新功能添加翻译，请于PR内额外附上文件，而不要加进代码。
   - 键名特别长也没事，符合当前风格就行，~~但是你别长到100层就行。~~
## 提交代码

1. **分支管理**
   - 从 `main` 分支创建新功能分支到你自己的仓库
   - 完成开发后，创建 PR 到 `main` 分支

2. **提交信息**
   - 使用清晰、简洁的提交信息
   - 提交信息格式：`:emoji: 提交信息`
   - Emoji 源于 https://gitmoji.js.org/
   - 如果有不止一个修改，请将最大的修改置于第一个，后面每行一个

3. **代码审查**
   - 所有代码更改都会经过代码审查（一般是我，有的时候会用AI）
   - 审查者会检查代码质量和潜在问题
   - 尽量在提交PR后的几天内多看GitHub，根据反馈进行必要地修改，如果长时间（一般是14天）没有修改，我将直接修改代码。

## 测试

该项目目前没有自动化测试，所有更改都应经过手动测试以确保功能正常。