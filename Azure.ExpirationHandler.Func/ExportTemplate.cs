using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.ResourceManager;
using Microsoft.Extensions.Configuration;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class ExportTemplate
    {
        [FunctionName("ExportTemplate")]
        public static async Task Run([QueueTrigger("export-template", Connection = "ExportTemplateStorageQueueConnection")]string myQueueItem, ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            var expirationTagKey = config["ExpirationTagKey"];
            var authenticatedStub = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud));
            var subList = await authenticatedStub.Subscriptions.ListAsync();
            log.LogInformation($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");

            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            string rgName = data.ResourceGroupName.ToString();
            string subscriptionId = data.SubscriptionId.ToString();

            var azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud)).WithSubscription(subscriptionId);

            var exportResult = await azr.ResourceGroups.GetByName(rgName).ExportTemplateAsync(ResourceGroupExportTemplateOptions.IncludeBoth);

            if (exportResult.Error != null)
            {

            }

            var templateJson = exportResult.TemplateJson;

        }
    }
}
