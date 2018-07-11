using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class GenerateTagSuite
    {
        private static IAzure _azr;

        [FunctionName("generate-tag-suite")]
        public static void Run([QueueTrigger("generate-tag-suite", Connection = "QueueStorageAccount")]string myQueueItem, TraceWriter log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            var subscription = data.SubscriptionId.Value;
            var dateCreated = data.DateCreated.Value;

            if (_azr == null || _azr.SubscriptionId != subscription)
            {
                log.Info($"Created a new instance of _azr for subscription {subscription}");
                _azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud)).WithSubscription(subscription);
            }
            SetTags(_azr, data.GroupName.Value, data.User.Value, dateCreated, log);
        }

        private static void SetTags(IAzure azr, string newResourceGroupName, string owner, string dateCreated, TraceWriter log)
        {
            var g = azr.ResourceGroups.GetByName(newResourceGroupName);
            var tags = new Dictionary<string, string>();
            if (g.Tags != null)
            {
                foreach (var t in g.Tags)
                {
                    tags.Add(t.Key, t.Value);
                }
            }
            var pieces = g.Name.Split('-');
            if (pieces.Length >= 4)
            {
                log.Info($"Working on {g.Name}");
                var function = pieces[0];
                var customer = pieces[1];
                var env = pieces[2];
                var name = string.Join("-", pieces.Skip(3));

                tags.Add("function", function);
                tags.Add("customer", customer);
                tags.Add("env", env);
                tags.Add("thing", name);
                tags.Add("owner", owner);
            };

            if (!tags.Any(x => x.Key == "expires")) //don't update/overwrite expiration tag if it exists
            {
                DateTime creationDate = DateTime.UtcNow;
                DateTime.TryParse(dateCreated, out creationDate);
                tags.Add("expires", creationDate.AddDays(30).ToString("o"));
            }

            foreach (var a in tags)
            {
                log.Info($"{a.Key}: {a.Value}");
            }

            g.Update().WithTags(tags).Apply();
        }
    }
}
