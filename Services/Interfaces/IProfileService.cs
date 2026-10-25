using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScopeCLI.Models;

namespace ScopeCLI.Services.Interfaces
{
    public interface IProfileService
    {
        Task<List<Profile>> GetAllProfilesAsync(CancellationToken cancellationToken = default);
        Task<Profile?> GetProfileByIdAsync(string shortId, CancellationToken cancellationToken = default);
        Task<Profile?> FindProfileByNameOrIdAsync(string query, CancellationToken cancellationToken = default);
        Task SaveProfileAsync(Profile profile, CancellationToken cancellationToken = default);
        Task<bool> DeleteProfileAsync(string shortId, CancellationToken cancellationToken = default);
        Task SaveStateAsync(Profile? profile, CancellationToken cancellationToken = default);
        Task RestoreStateAsync(Profile? profile, CancellationToken cancellationToken = default);
        Task SwitchProfileAsync(Profile? oldProfile, Profile? newProfile, CancellationToken cancellationToken = default);
        string GenerateUniqueShortId();
    }
}
