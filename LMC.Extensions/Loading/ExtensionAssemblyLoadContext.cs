using System.Reflection;
using System.Runtime.Loader;

namespace LMC.Extensions.Loading;

internal sealed class ExtensionAssemblyLoadContext(string mainAssemblyPath, IEnumerable<string> dependencyDirectories)
    : AssemblyLoadContext($"LMC.Extensions::{Path.GetFileNameWithoutExtension(mainAssemblyPath)}", isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new(mainAssemblyPath);
    private readonly IReadOnlyList<string> _dependencyDirectories = dependencyDirectories.ToList().AsReadOnly();

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var resolvedPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (resolvedPath != null)
        {
            return LoadFromAssemblyPath(resolvedPath);
        }

        foreach (var dependencyDirectory in _dependencyDirectories)
        {
            var candidatePath = Path.Combine(dependencyDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(candidatePath))
            {
                return LoadFromAssemblyPath(candidatePath);
            }
        }

        return AppDomain.CurrentDomain
            .GetAssemblies()
            .FirstOrDefault(assembly => string.Equals(
                assembly.GetName().Name,
                assemblyName.Name,
                StringComparison.OrdinalIgnoreCase));
    }
}
