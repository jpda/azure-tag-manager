using System;
using System.Collections.Generic;
using System.Linq;
using Azure.ExpirationHandler.Func;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using static Microsoft.Azure.Management.Fluent.Azure;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public class GenerateTagSuite : AzureAuthenticatedBase
    {
        private readonly TaggingOptions _options;

        public GenerateTagSuite(IAuthenticated authenticatedStub, ILoggerFactory loggerFactory, IOptions<TaggingOptions> options) : base(authenticatedStub, loggerFactory)
        {
            _options = options.Value;
        }

        private IAzure _azr;

        [FunctionName("generate-tag-suite")]
        public void Run([QueueTrigger("generate-tag-suite", Connection = "QueueStorageAccount")]string myQueueItem)
        {
            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            var subscription = data.SubscriptionId.Value;
            var dateCreated = data.DateCreated.Value;
            
            _azr = _authenticatedStub.WithSubscription(subscription);

            var group = data.GroupName.Value;
            var user = data.User.Value ?? _options.DefaultOwner;
            var expirationWindow = TimeSpan.FromDays(_options.ExpirationWindowInDays);
            _log.LogInformation($"starting: {group}, {user}, {dateCreated.ToString()}, {expirationWindow}");
            SetTags(group, user, dateCreated.ToString(), expirationWindow, _options.DefaultTaggedByName);
        }

        private void SetTags(string newResourceGroupName, string owner, string dateCreated, TimeSpan expirationWindow, string identity)
        {
            var g = _azr.ResourceGroups.GetByName(newResourceGroupName);
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
                _log.LogInformation($"Working on {g.Name}");
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
                tags.AddOrUpdate("tagged-by", identity);
            };

            var creationDate = DateTime.UtcNow;
            DateTime.TryParse(dateCreated, out creationDate);
            // don't update/overwrite expiration tag if it exists
            tags.AddOrKeepExisting(_options.ExpirationTagKey, creationDate.Add(expirationWindow).ToString("o"));

            foreach (var a in tags)
            {
               _log.LogInformation($"{a.Key}: {a.Value}");
            }

            g.Update().WithTags(tags).Apply();
        }
    }
}