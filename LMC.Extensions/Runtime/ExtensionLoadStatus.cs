namespace LMC.Extensions.Runtime;

public enum ExtensionLoadStatus
{
    Loaded,
    SkippedDisabled,
    SkippedInvalidPackage,
    SkippedIncompatibleLauncher,
    SkippedMissingHardDependency,
    FailedToLoad
}
