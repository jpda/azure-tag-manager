using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class GenerateTagSuite
    {
        private static IAzure _azr;

        [FunctionName("generate-tag-suite")]
        public static void Run([QueueTrigger("generate-tag-suite", Connection = "QueueStorageAccount")]string myQueueItem, ILogger log, ExecutionContext context)
        {
            var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();
            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            var subscription = data.SubscriptionId.Value;
            var dateCreated = data.DateCreated.Value;
            int.TryParse(config["ExpirationWindowInDays"], out int expirationWindowInDays);

            //todo: something is broken here
            //log.Info($"{_azr}");
            //if (_azr == null || _azr.SubscriptionId != subscription)
            //{
            log.LogInformation($"Created a new instance of _azr for subscription {subscription}");
            var azCredential = new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud);
            // todo: eww - should really find a better way to get the identity out of the IAzure interface
            _azr = Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(azCredential).WithSubscription(subscription);
            //}
            var group = data.GroupName.Value;
            var user = data.User.Value ?? "unknown";
            var expirationWindow = TimeSpan.FromDays(expirationWindowInDays);
            log.LogInformation($"starting: {group}, {user}, {dateCreated.ToString()}, {expirationWindow}");
            SetTags(_azr, group, user, dateCreated.ToString(), expirationWindow, log);
        }

        private static void SetTags(IAzure azr, string newResourceGroupName, string owner, string dateCreated, TimeSpan expirationWindow, ILogger log, string identity = "azman")
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
                log.LogInformation($"Working on {g.Name}");
                var function = pieces[0];
                var customer = pieces[1];
                var env = pieces[2];
                var name = string.Join("-", pieces.Skip(3));

                // todo: figure out a good way to map these oob so you can come up with your own nomenclature
                tags.AddOrUpdate("function", function);
                tags.AddOrUpdate("customer", customer);
                tags.AddOrUpdate("env", env);
                tags.AddOrUpdate("thing", name);
                tags.AddOrUpdate("owner", owner);
                tags.AddOrUpdate("tagged-by", identity ?? "azman");
            };

            DateTime creationDate = DateTime.UtcNow;
            DateTime.TryParse(dateCreated, out creationDate);
            //don't update/overwrite expiration tag if it exists
            // todo: push tag key to configuration
            tags.AddOrKeepExisting("expires", creationDate.Add(expirationWindow).ToString("o"));

            foreach (var a in tags)
            {
                log.LogInformation($"{a.Key}: {a.Value}");
            }

            g.Update().WithTags(tags).Apply();
        }
    }

    public static class DictionaryExtensions
    {
        public static void AddOrUpdate<K, V>(this Dictionary<K, V> dict, K key, V val)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = val;
                return;
            }
            dict.Add(key, val);
        }

        public static void AddOrKeepExisting<K, V>(this Dictionary<K, V> dict, K key, V val)
        {
            if (dict.ContainsKey(key)) return;
            dict.Add(key, val);
        }
    }
}
