using System.Threading;
using System.Threading.Tasks;

namespace ScopeCLI.Services.Interfaces
{
    public record OptimizationResult(int TrimmedProcessesCount, int TerminatedProcessesCount, long ApproximateFreedMemoryBytes);

    public interface ISystemOptimizer
    {
        Task<OptimizationResult> OptimizeAsync(bool aggressiveKill = false, CancellationToken cancellationToken = default);
    }
}
