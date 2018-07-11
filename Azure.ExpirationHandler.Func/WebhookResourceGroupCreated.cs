
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using System.Threading.Tasks;

namespace Azure.ExpirationHandler.Func
{
    public static class WebhookResourceGroupCreated
    {
        [FunctionName("webhook-rg-created")]
        public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log, [Queue("generate-tag-suite", Connection = "QueueStorageAccount")]IAsyncCollector<string> outputQueueItem)
        {
            string requestBody = new StreamReader(req.Body).ReadToEnd();
            dynamic root = JsonConvert.DeserializeObject(requestBody);
            if (root == null)
            {
                return new BadRequestObjectResult("No root element");
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
