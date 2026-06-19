// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using LMCCore.Account;
using LMCCore.Game.Launching.Execution;
using LMCCore.Game.Model;

namespace LMCCore.Game.Launching.Steps.PreLaunch;

public sealed class PreLaunchCheckStepHandler : IGameLaunchStepHandler
{
    public GameLaunchProgressStep Step => GameLaunchProgressStep.PreLaunchCheck;

    public Task ExecuteAsync(GameLaunchContext context, CancellationToken cancellationToken)
    {
        if (context.Config.JavaPaths.Count == 0)
        {
            JavaNotFoundException.Throw();
        }
        
        if (AccountManager.Accounts.Count == 0)
        {
            AccountNotFoundException.Throw();
        }
        
        if ((string.IsNullOrEmpty(context.Version.JarPath) || !File.Exists(context.Version.JarPath)) ||
            (string.IsNullOrEmpty(context.Version.JsonPath) || !File.Exists(context.Version.JsonPath)))
        {
            throw new VersionInvalidException("版本Jar或Json缺失");
        }

        if (!string.IsNullOrEmpty(context.Version.VersionInfo?.InheritsFrom))
        {
            throw new NotImplementedException();
            //TODO: 版本继承
        }
        
        if (context.Version.Status != VersionStatus.Valid)
        {
            throw new VersionInvalidException($"{nameof(context.Version.Status)} 不为 Valid");
        }
        return Task.CompletedTask;
    }
}

public class JavaNotFoundException : Exception
{
    private JavaNotFoundException(string msg) : base(msg) { }
    public static void Throw()
    {
        throw new JavaNotFoundException("无可用Java");
    }
}

public class AccountNotFoundException : Exception
{
    private AccountNotFoundException(string msg) : base(msg) { }
    public static void Throw()
    {
        throw new AccountNotFoundException("无可用账户");
    }   
}

public class VersionInvalidException(string msg) : Exception(msg);