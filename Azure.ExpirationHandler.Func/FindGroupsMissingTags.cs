using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class FindGroupsMissingTags
    {
        [FunctionName("find-groups-missing-tags")]
        public static async Task RunAsync([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log, ExecutionContext context, [Queue("generate-tag-suite", Connection = "QueueStorageAccount")]IAsyncCollector<string> outputQueueItem)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            var expirationTagKey = config["ExpirationTagKey"];
            var azCredential = new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud);
            var authenticatedStub = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(azCredential);
            var subList = await authenticatedStub.Subscriptions.ListAsync();
            log.LogInformation($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");

            foreach (var s in subList)
            {
                // todo: figure out why this doesn't work. pretty odd tbh
                //var azr = authenticatedStub.WithSubscription(s.SubscriptionId);
                var azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud)).WithSubscription(s.SubscriptionId);
                var groupResponse = await azr.ResourceGroups.ListAsync(true);
                // todo: should this really be the indicator of an untagged or unmanaged/unknown resource group? dunno. maybe it's tagged-by, maybe it's just expiration date.
                var groups = groupResponse.Where(x => x.Tags == null || !x.Tags.ContainsKey(expirationTagKey)).ToList();
                foreach (var g in groups)
                {
                    await outputQueueItem.AddAsync(JsonConvert.SerializeObject(new
                    {
                        Operation = "scan-tags-not-found",
                        GroupName = g.Name,
                        User = azCredential.ClientId,
                        azr.SubscriptionId,
                        DateCreated = DateTime.UtcNow
                    }));
                    log.LogInformation($"Sending {azr.SubscriptionId}: {g.Name} to tagging");
                }
            }
        }
    }
}