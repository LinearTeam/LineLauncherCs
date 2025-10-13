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