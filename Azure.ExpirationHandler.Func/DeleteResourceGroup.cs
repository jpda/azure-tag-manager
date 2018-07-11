using System;
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
    public static class DeleteResourceGroup
    {
        private static IAzure _azr;

        [FunctionName("delete-resource-group")]
        public static void Run([QueueTrigger("delete-resource-group", Connection = "QueueStorageAccount")]string myQueueItem, TraceWriter log, ExecutionContext context)
        {
            log.Info($"C# Queue trigger function processed: {myQueueItem}");
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            var subscription = data.SubscriptionId.Value;
            var resourceGroupName = data.ResourceGroupName.Value;

            if (_azr == null || _azr.SubscriptionId != subscription)
            {
                log.Info($"Created a new instance of _azr for subscription {subscription}");
                _azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud)).WithSubscription(subscription);
            }
            log.Info($"Deleting resource group {resourceGroupName}");
            _azr.ResourceGroups.BeginDeleteByName(resourceGroupName);
        }
    }
}
