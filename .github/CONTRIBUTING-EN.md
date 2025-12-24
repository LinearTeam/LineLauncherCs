*对于中文版，请见 [CONTRIBUTING.md](./CONTRIBUTING.md).*
# Contribution Guide

Actually, this guide is more for my own reference, but contributions of any form are welcome. Even if you don't know how to code, opening an issue or PR when you encounter a problem is a form of support for this project.

## Localization
Please go to [Crowdin](https://crowdin.com/project/linelaunchercs) for more information.

If you want the project to support a new language, please open an issue **in GitHub**. It will be considered for inclusion after discussion.

If you already have a language file translated for a new language, we will do our best to include it to avoid wasting the translator's effort.

## Development Environment Setup

1. Ensure .NET (8.0, may be updated) is installed.
2. Clone the repository locally.
3. Open the `LineLauncherCs.sln` solution using an IDE (e.g. Visual Studio, JetBrains Rider and more).
4. Restore NuGet package dependencies.
5. Build the solution to ensure everything is working correctly.

## Coding Style

### General Rules

```text
PascalCase: First letter capitalized, each subsequent word starts with a capital letter.
camelCase: First letter lowercase, each subsequent word starts with a capital letter.
```

1. **Naming Conventions**
    - Use PascalCase for class names, method names, and property names.
    - Use camelCase for private fields, prefixed with `_`.
    - Use PascalCase for constants.
    - Use the `s_` prefix for static private fields.
    - Keep naming rules for `protected` consistent with `private`.
    - Interface names should start with a capital letter `I`, followed by PascalCase. *~~However, as of writing this, there is only one `interface` in this project.~~*

2. **Code Formatting**
    - Use 4 spaces for indentation (recommended to modify IDE settings).
    - Place curly braces `{}` on a new line.
    - Keep each line of code under 120 characters.
    - Use blank lines to separate logical blocks.

3. **Comments**
    - Comments are encouraged, but they don't need to be overly detailed. They can also be small remarks or notes, in either Chinese or English, as long as they are understandable.
    - Minimize the use of XML documentation comments unless there are special circumstances to explain.

4. **Exception Handling**
    - Appropriately catch exceptions for code that may throw them.
    - Use specific exception types rather than generic exceptions (although using them is acceptable).
    - After catching an exception, provide meaningful error information in the log.

### Project Structure Conventions

1. **Namespaces**
    - Follow the project structure naming, e.g., `LMC.Basic.Configs`.
    - Do not create overly deep namespaces. For example, taking `LMCUI.Pages.AccountPage.AddAccount.Offline` as an example, `GameIdWarnStep` should not be placed in a deeper namespace. Exceptions are granted for UI namespaces that require extensive operations/pages, such as deeply nested settings pages, but this situation should be avoided as much as possible.

2. **File Organization**
    - Each class should be placed in a separate file, unless the class is particularly small, has repetitive functionality, or is a model class (`record`, `enum`, etc.).
    - The file name should match the class name.
    - Related functionalities should be placed in the same directory.

3. **Dependency Management**
    - Avoid circular dependencies as much as possible.
    - ~~Try to use dependency injection (just kidding, I don't use it either XD).~~

### UI Coding Standards

1. **XAML Files**
    - Use the `axaml` file extension.
    - ~~Create custom controls for reusable UI elements (well, I don't create them either XD, you can ignore this).~~
    - There is no need to follow patterns like MVVM, but do not write business code in the `LMCUI` project *(you can call business code within it, but pay attention to separating the UI thread and task threads)*. Instead, use the business code from `LMCCore` and `LMC`.

2. **Internationalization**

_Because Chinese is the primary language of this project,_  
_So if you are adding new user-visible strings, but you don't speak Chinese,_  
_Please add an extra file of `LMCUI/Languages/en-US.xml` or other language's translation file that contains your new strings,_  
_We will translate it into Chinese after passed code reviewing._   

**DO NOT PUSH ANY LANGUAGE FILE into your commits excepted `zh-CN.xml`.**

* All user-visible strings should be obtained through `I18nManager`. Error messages that users are expected to encounter should also be handled this way, except for some error messages that users are not expected to trigger.
* Newly added strings must be added to the `zh-CN.xml` language file. **Do not add other languages in the code!** Translations are managed via [Crowdin](https://crowdin.com/project/linelaunchercs) upon release. If you really want to add translations for your new feature, please attach the additional files in the PR instead of adding them to the code.
* It's okay if the key names are particularly long, as long as they conform to the current style. ~~But please don't make them 100 layers deep.~~

## Submitting Code

1. **Branch Management**
    - Create a new feature branch from the `main` branch in your own repository.
    - After development is complete, create a PR to the `main` branch.

2. **Commit Messages**
    - Use clear and concise commit messages.
    - Commit message format: `:emoji: Commit message`
    - Emojis are sourced from https://gitmoji.js.org/
    - If there is more than one modification, place the largest modification first, followed by one per line.

3. **Code Review**
    - All code changes will undergo code review (usually by me, sometimes with AI assistance).
    - Reviewers will check code quality and potential issues.
    - Try to check GitHub frequently within a few days after submitting a PR and make necessary modifications based on feedback. If no modifications are made for an extended period (generally 14 days), I will directly modify the code.

## Testing

This project currently does not have automated testing. All changes should be manually tested to ensure functionality is correct.