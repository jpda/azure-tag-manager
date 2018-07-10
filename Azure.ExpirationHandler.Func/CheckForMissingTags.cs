using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace Azure.ExpirationHandler.Func
{
    public static class CheckForMissingTags
    {
        [FunctionName("check-missing-tags")]
        public static void Run([TimerTrigger("0 */65 * * * *")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
