using System.Runtime.Loader;
using LMC.Extensions.Abstractions;
using LMC.Extensions.Resolution;

namespace LMC.Extensions.Loading;

public class LoadedLMCExtension
{
    public required ResolvedExtension ResolvedExtension { get; init; }

    public required ILMCExtension Instance { get; init; }

    public required string CacheDirectory { get; init; }

    public required AssemblyLoadContext LoadContext { get; init; }
}
