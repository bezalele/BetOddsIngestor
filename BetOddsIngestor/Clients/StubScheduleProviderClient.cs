using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BetOddsIngestor.Clients
{
    /// <summary>
    /// Temporary stub schedule provider.
    /// Replace with real implementation later.
    /// </summary>
    public sealed class StubScheduleProviderClient : IScheduleProviderClient
    {
        public Task<IReadOnlyList<ScheduleGameDto>> GetScheduleAsync(DateTime fromUtc, DateTime toUtc)
        {
            // FOR NOW: return empty list so app runs without error.
            // Later we will plug in a real NBA schedule feed.
            return Task.FromResult<IReadOnlyList<ScheduleGameDto>>(Array.Empty<ScheduleGameDto>());
        }
    }
}
