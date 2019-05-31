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
    public class DeleteResourceGroup : AzureAuthenticatedBase
    {
        private IAzure _azr;
        private readonly bool _commit;

        public DeleteResourceGroup(IAuthenticated auth, ILoggerFactory loggerFactory, IOptions<DeletionOptions> options) : base(auth, loggerFactory)
        {
            _commit = options.Value.Commit;
        }

        [FunctionName("delete-resource-group")]
        public void Run([QueueTrigger("delete-resource-group", Connection = "QueueStorageAccount")]string myQueueItem)
        {
            dynamic data = JsonConvert.DeserializeObject(myQueueItem);
            var subscription = data.SubscriptionId.Value;
            var resourceGroupName = data.ResourceGroupName.Value;

            if (_azr == null || _azr.SubscriptionId != subscription)
            {
                _log.LogInformation($"Created a new instance of _azr for subscription {subscription}");
                _azr = _authenticatedStub.WithSubscription(subscription);
            }
            _log.LogInformation($"Deleting resource group {resourceGroupName}");

            _log.LogInformation($"Commit: {_commit}");
            if (_commit)
            {
                _azr.ResourceGroups.BeginDeleteByName(resourceGroupName);
            }
        }
    }
}
