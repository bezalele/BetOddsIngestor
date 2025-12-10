using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BetOddsIngestor.Clients
{
    public interface IScheduleProviderClient
    {
        /// <summary>
        /// Returns all NBA games between [fromUtc, toUtc).
        /// StartTimeUtc on each game must be UTC.
        /// </summary>
        Task<IReadOnlyList<ScheduleGameDto>> GetScheduleAsync(DateTime fromUtc, DateTime toUtc);
    }
}
