using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class FindExpiredGroups
    {
        [FunctionName("find-expired-groups")]
        public static async Task Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, TraceWriter log, [Queue("delete-resource-group", Connection = "QueueStorageAccount")]IAsyncCollector<string> expiredGroups, ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            var expirationTagKey = config["ExpirationTagKey"];
            var authenticatedStub = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud));
            var subList = await authenticatedStub.Subscriptions.ListAsync();
            log.Info($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");
            foreach (var s in subList)
            {
                try
                {
                    // todo: figure out why this doesn't work. pretty odd tbh
                    //var azr = authenticatedStub.WithSubscription(s.SubscriptionId);
                    var azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud)).WithSubscription(s.SubscriptionId);
                    var groupResponse = await azr.ResourceGroups.ListAsync(true);
                    var groupsToDelete = groupResponse.Where(x => x.Tags != null && x.Tags.ContainsKey(expirationTagKey) && DateTime.Parse(x.Tags[expirationTagKey]) < DateTime.UtcNow).ToList();

                    foreach (var g in groupsToDelete.Select(x => new { azr.SubscriptionId, ResourceGroupName = x.Name, ExpirationDate = x.Tags[expirationTagKey] }))
                    {
                        log.Info($"RG {g.ResourceGroupName} queuing for deletion; expired at {g.ExpirationDate}");
                        await expiredGroups.AddAsync(JsonConvert.SerializeObject(g));
                    }
                }
                catch (Exception ex)
                {

                    log.Error(ex.Message, ex);
                }

            }
        }
    }
}
