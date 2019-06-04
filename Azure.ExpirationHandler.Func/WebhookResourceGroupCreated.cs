
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Azure.ExpirationHandler.Func
{
    public static class WebhookResourceGroupCreated
    {
        [FunctionName("webhook-rg-created")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, ILogger log, [Queue("generate-tag-suite", Connection = "QueueStorageAccount")]IAsyncCollector<string> outputQueueItem)
        {
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic root = JsonConvert.DeserializeObject(requestBody);
            log.LogInformation(requestBody);
            if (root == null)
            {
                return new BadRequestObjectResult("No root element");
            }

            if (root.data.context.activityLog.subStatus.Value.ToString() != "Created") // activity log alerts are bogus - 'Create Resource Group' doesn't work so we have to check ourselves
            {
                return (ActionResult)new OkResult();
            }

            await outputQueueItem.AddAsync(JsonConvert.SerializeObject(
                new
                {
                    Operation = root.data.context.activityLog.operationName,
                    GroupName = root.data.context.activityLog.resourceGroupName,
                    User = root.data.context.activityLog.caller,
                    SubscriptionId = root.data.context.activityLog.subscriptionId,
                    DateCreated = root.data.context.activityLog.eventTimestamp
                }));

            return (ActionResult)new OkResult();
        }
    }
}
