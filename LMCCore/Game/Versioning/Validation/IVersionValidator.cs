using LMCCore.Game.Model;
using LMCCore.Game.Model.Validation;

namespace LMCCore.Game.Versioning.Validation;

public interface IVersionValidator
{
    Task<VersionValidationResult> ValidateAsync(LocalGameVersionEntry version, CancellationToken cancellationToken = default);
}
