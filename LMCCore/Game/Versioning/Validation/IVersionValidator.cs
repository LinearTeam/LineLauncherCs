using LMCCore.Game.Model;

namespace LMCCore.Game.Versioning.Validation;

public interface IVersionValidator
{
    Task<VersionValidationResult> ValidateAsync(LocalGameVersionEntry version, CancellationToken cancellationToken = default);
}
