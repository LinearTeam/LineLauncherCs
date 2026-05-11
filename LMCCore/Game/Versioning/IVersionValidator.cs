using LMCCore.Game.Model;

namespace LMCCore.Game.Versioning;

public interface IVersionValidator
{
    Task<VersionValidationResult> ValidateAsync(LocalGameVersionEntry version, CancellationToken cancellationToken = default);
}
