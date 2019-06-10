using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.ExpirationHandler.Func;
using Azure.ExpirationHandler.Func.Models;
using Azure.ExpirationHandler.Models;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Management.Graph.RBAC.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Microsoft.Azure.Management.Fluent.Azure;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public class DeletionOrchestrator : AzureAuthenticatedBase
    {
        private readonly DeletionOptions _options;

        public DeletionOrchestrator(IAuthenticated auth, ILoggerFactory loggerFactory, IOptions<DeletionOptions> options) : base(auth, loggerFactory)
        {
            _options = options.Value;
        }

        [FunctionName("DeletionOrchestrator_QueueStart")]
        public static async Task QueueStart([QueueTrigger("%DeleteResourceGroupQueueName%", Connection = "DeleteQueueStorageAccount")] DeleteResourceGroupRequest deleteQueueItem, [OrchestrationClient] DurableOrchestrationClient starter, ILogger log)
        {
            string instanceId = await starter.StartNewAsync("DeletionOrchestrator", deleteQueueItem);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        [FunctionName("DeletionOrchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context)
        {
            var request = context.GetInput<DeleteResourceGroupRequest>();
            if (_options.ExportTemplateBeforeDeletion)
            {
                var exportRequest = await context.CallActivityAsync<bool>("DeletionOrchestrator_ExportTemplate", request);
                if (!exportRequest && _options.RequireExport)
                {
                    _log.LogWarning($"Export failed for {request.ResourceGroupName} but export is required.");
                    return;
                }
            }

            if (_options.NotifyOnDeletion)
            {
                await context.CallActivityAsync("DeletionOrchestrator_NotifyOnDelete", request);
            }

            var deletionRequest = await context.CallActivityAsync<DeleteResourceGroupRequest>("DeletionOrchestrator_DeleteResourceGroup", request);
        }

        [FunctionName("DeletionOrchestrator_ExportTemplate")]
        public async Task<bool> ExportTemplate([ActivityTrigger]DeleteResourceGroupRequest request, Binder binder)
        {
            var azr = _authenticatedStub.WithSubscription(request.SubscriptionId);
            var exportResult = await azr.ResourceGroups.GetByName(request.ResourceGroupName).ExportTemplateAsync(ResourceGroupExportTemplateOptions.IncludeBoth);

            if (exportResult.Error != null)
            {
                _log.LogError($"ExportTemplate error: {exportResult.Error.Code} {exportResult.Error.Message} at {exportResult.Error.Target}");
                return false;
            }

            var attributes = new Attribute[]
               {
                    new BlobAttribute($"{_options.PersistenceStorageContainer}/{DateTime.UtcNow.ToString("yyyy-MM-dd")}/{request.ResourceGroupName}.json"),
                    new StorageAccountAttribute("ExportTemplateBlobStorageConnection")
               };

            using (var writer = await binder.BindAsync<TextWriter>(attributes).ConfigureAwait(false))
            {
                writer.Write(exportResult.TemplateJson);
            }

            return true;
        }

        [FunctionName("DeletionOrchestrator_NotifyOnDelete")]
        public async Task NotifyOnDelete([ActivityTrigger]DeleteResourceGroupRequest request, [Queue("%OutboxQueueName%", Connection = "OutboxQueueStorageAccount")]IAsyncCollector<MailInfo> outboundMail)
        {
            var azr = _authenticatedStub.WithSubscription(request.SubscriptionId);
            var resourceGroup = azr.ResourceGroups.GetByName(request.ResourceGroupName);

            var targets = new List<string>();

            if (resourceGroup.Tags.ContainsKey("owner") && resourceGroup.Tags.TryGetValue("owner", out var ownerMail))
            {
                targets.Add(ownerMail);
            }

            // find the role definition first - using built in 'owner' for now
            var o = await _authenticatedStub.RoleDefinitions.GetByScopeAndRoleNameAsync(resourceGroup.Id, "Owner");

            // find assignments on the RG matching the owner definition
            var owners = _authenticatedStub.RoleAssignments.ListByScope(resourceGroup.Id).Where(x => x.RoleDefinitionId == o.Id);

            foreach (var a in owners)
            {
                var mail = GetMailForPrincipal(a.PrincipalId);
                if (!string.IsNullOrEmpty(mail))
                {
                    targets.Add(mail);
                }
            }

            // todo: templatize the mail, move it out entirely
            var mailMessage = new MailInfo()
            {
                Subject = "The azman cometh...",
                To = targets,
                MailBody = $"<h1>...and taketh away.</h1><h2>azman deletion pass at {DateTime.UtcNow.ToString("O")} deleted {request.ResourceGroupName} from subscription {request.SubscriptionId}.</h2><p>You're getting this notification as an owner of a child or parent resource.</p>"
            };

            await outboundMail.AddAsync(mailMessage);
        }

        private string GetMailForPrincipal(string principalId)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var user = _authenticatedStub.ActiveDirectoryUsers.GetById(principalId);
                return user.Mail;
            }
            catch (GraphErrorException ex)
            {
                // if not found, search for SP and groups? not sure, since we wouldn't notify a service principal
                // there's not a discernable way to figure out the type of principal first, without searching the graph outside of the management sdk
                // although there might be an argument here for ditching this and just going to the graph on our own
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    try
                    {
                        var g = _authenticatedStub.ActiveDirectoryGroups.GetById(principalId);
                        return g.Mail;
                    }
                    catch
                    {
                        // toss, since it's an SP and we don't care about SPs. not sure if we should even log this exception, although there may be other types of interest
                        _log.LogError(ex, ex.Message);
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
        }

        [FunctionName("DeletionOrchestrator_DeleteResourceGroup")]
        public void DeleteResourceGroup([ActivityTrigger]DeleteResourceGroupRequest request)
        {
            var azr = _authenticatedStub.WithSubscription(request.SubscriptionId);

            if (azr == null || azr.SubscriptionId != request.SubscriptionId)
            {
                _log.LogInformation($"Created a new instance of _azr for subscription {request.SubscriptionId}");
                azr = _authenticatedStub.WithSubscription(request.SubscriptionId);
            }

            _log.LogInformation($"Deleting resource group {request.ResourceGroupName}");
            _log.LogInformation($"Commit: {_options.Commit}");

            if (_options.Commit)
            {
                azr.ResourceGroups.BeginDeleteByName(request.ResourceGroupName);
            }
        }
    }
}