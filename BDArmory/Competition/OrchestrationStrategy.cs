using System;
using System.Collections;
using BDArmory.Competition.RemoteOrchestration;
namespace BDArmory.Competition
{
    public interface OrchestrationStrategy
    {
        /// <summary>
        /// Part 2 of Remote Orchestration
        ///
        /// Receives a pre-configured environment with spawned craft ready to fly.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="service"></param>
        /// <returns></returns>
        public IEnumerator Execute(BDAScoreClient client, BDAScoreService service);
    }
}
