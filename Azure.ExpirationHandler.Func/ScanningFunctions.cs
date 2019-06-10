using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Extensions.Options;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Azure.ExpirationHandler.Func;
using Azure.ExpirationHandler.Func.Models;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public class ScanningFunctions : AzureAuthenticatedBase
    {
        private readonly string _expirationTagKey;
        private readonly TaggingOptions _options;

        public ScanningFunctions(Microsoft.Azure.Management.Fluent.Azure.IAuthenticated auth, ILoggerFactory loggerFactory, IOptions<TaggingOptions> options) : base(auth, loggerFactory)
        {
            _options = options.Value;
            _expirationTagKey = _options.ExpirationTagKey;
        }

        [FunctionName("find-groups-missing-tags")]
        public async Task FindMissing([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log, [Queue("generate-tag-suite", Connection = "QueueStorageAccount")]IAsyncCollector<string> outputQueueItem)
        {
            var subList = await _authenticatedStub.Subscriptions.ListAsync();
            log.LogInformation($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");

            foreach (var s in subList)
            {
                var azr = _authenticatedStub.WithSubscription(s.SubscriptionId);
                var groupResponse = await azr.ResourceGroups.ListAsync(true);
                // todo: should this really be the indicator of an untagged or unmanaged/unknown resource group? dunno. maybe it's tagged-by, maybe it's just expiration date.
                var groups = groupResponse.Where(x => x.Tags == null || !x.Tags.ContainsKey(_expirationTagKey)).ToList();
                foreach (var g in groups)
                {
                    await outputQueueItem.AddAsync(JsonConvert.SerializeObject(new
                    {
                        Operation = "scan-tags-not-found",
                        GroupName = g.Name,
                        User = _options.DefaultOwner,
                        azr.SubscriptionId,
                        DateCreated = DateTime.UtcNow
                    }));
                    log.LogInformation($"Sending {azr.SubscriptionId}: {g.Name} to tagging");
                }
            }
        }

        [FunctionName("notify-upcoming")]
        public async Task FindUpcoming([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, [Queue("%OutboxQueueName%", Connection = "OutboxQueueConnection")]IAsyncCollector<MailInfo> outboundMail)
        {
            var subList = await _authenticatedStub.Subscriptions.ListAsync();
            _log.LogInformation($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");
            foreach (var s in subList)
            {
                try
                {
                    var azr = _authenticatedStub.WithSubscription(s.SubscriptionId);
                    var groupResponse = await azr.ResourceGroups.ListAsync(true);
                    var groupsUpcomingDeletion = groupResponse.Where(x => x.Tags != null && x.Tags.ContainsKey(_expirationTagKey) && DateTime.Parse(x.Tags[_expirationTagKey]) < DateTime.UtcNow.AddDays(-7)).ToList();

                    foreach (var g in groupsUpcomingDeletion.Select(x => new { azr.SubscriptionId, ResourceGroupName = x.Name, ExpirationDate = x.Tags[_expirationTagKey] }))
                    {
                        _log.LogInformation($"RG {g.ResourceGroupName} scheduled for deletion; expires on {g.ExpirationDate}");
                        await outboundMail.AddAsync();
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message, ex);
                }
            }
        }

        [FunctionName("find-expired-groups")]
        public async Task FindExpired([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, [Queue("delete-resource-group", Connection = "QueueStorageAccount")]IAsyncCollector<string> expiredGroups)
        {
            var subList = await _authenticatedStub.Subscriptions.ListAsync();
            _log.LogInformation($"Found {subList.Count()} subscriptions: { string.Join(", ", subList.Select(x => x.SubscriptionId))}");
            foreach (var s in subList)
            {
                try
                {
                    var azr = _authenticatedStub.WithSubscription(s.SubscriptionId);
                    var groupResponse = await azr.ResourceGroups.ListAsync(true);
                    var groupsToDelete = groupResponse.Where(x => x.Tags != null && x.Tags.ContainsKey(_expirationTagKey) && DateTime.Parse(x.Tags[_expirationTagKey]) < DateTime.UtcNow).ToList();

                    foreach (var g in groupsToDelete.Select(x => new { azr.SubscriptionId, ResourceGroupName = x.Name, ExpirationDate = x.Tags[_expirationTagKey] }))
                    {
                        _log.LogInformation($"RG {g.ResourceGroupName} queuing for deletion; expired at {g.ExpirationDate}");
                        await expiredGroups.AddAsync(JsonConvert.SerializeObject(g));
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex.Message, ex);
                }
            }
        }
    }
}