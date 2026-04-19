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

using LMC;

namespace LMCCore.Java;

public class JavaHelper
{
    public async static Task<LocalJava?> GetRequiredJava(JavaCondition condition)
    {
        if (Current.Config?.JavaPaths == null) return null;
        foreach (var path in Current.Config.JavaPaths)
        {
            var info = await JavaManager.GetJavaInfo(path);
            if (condition.Test(info))
            {
                return info;
            }
        }
        return null;
    }
}


public class JavaCondition
{
    private Version? _minVersion;
    private Version? _maxVersion;
    private string? _implementor;
    private bool? _isJdk; 

    public JavaCondition Min(Version minVersion)
    {
        _minVersion = minVersion;
        return this;
    }

    public JavaCondition Max(Version maxVersion)
    {
        _maxVersion = maxVersion;
        return this;
    }

    public JavaCondition Implementor(string implementor)
    {
        _implementor = implementor;
        return this;
    }

    public JavaCondition IsJdk(bool required)
    {
        _isJdk = required;
        return this;
    }

    public bool Test(LocalJava? java)
    {
        if (java == null) return false;

        if (_minVersion != null && java.Version < _minVersion) return false;
        if (_maxVersion != null && java.Version > _maxVersion) return false;

        if (_implementor != null && 
            !string.Equals(java.Implementor, _implementor, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (_isJdk.HasValue && java.IsJdk != _isJdk.Value) return false;

        return true;
    }
}