using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BetOddsIngestor.Clients
{
    public interface IResultsProviderClient
    {
        /// <summary>
        /// Returns completed games (with scores) between [fromUtc, toUtc).
        /// Only Final / Completed games should be returned.
        /// </summary>
        Task<IReadOnlyList<ScoreGameDto>> GetScoresAsync(DateTime fromUtc, DateTime toUtc);
    }
}
