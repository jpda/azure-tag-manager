using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Azure.ExpirationHandler.Func;
using Azure.ExpirationHandler.Types;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.Graph.RBAC.Fluent.Models;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public class DeletionOrchestrator : AzureAuthenticatedBase
    {
        private IAzure _azr;
        private readonly DeletionOptions _options;

        public DeletionOrchestrator(Microsoft.Azure.Management.Fluent.Azure.IAuthenticated auth, ILoggerFactory loggerFactory, IOptions<DeletionOptions> options) : base(auth, loggerFactory)
        {
            _options = options.Value;
        }

        [FunctionName("DeletionOrchestrator_QueueStart")]
        public static async Task QueueStart([QueueTrigger("export-template", Connection = "ExportTemplateStorageQueueConnection")]DeleteResourceGroupRequest exportQueueItem, [OrchestrationClient]DurableOrchestrationClient starter, ILogger log)
        {
            string instanceId = await starter.StartNewAsync("DeletionOrchestrator", exportQueueItem);
            log.LogInformation($"Started orchestration with ID = '{instanceId}'.");
        }

        [FunctionName("DeletionOrchestrator")]
        public async Task RunOrchestrator([OrchestrationTrigger] DurableOrchestrationContext context, DeleteResourceGroupRequest data)
        {
            if (_options.ExportTemplateBeforeDeletion)
            {
                var exportRequest = await context.CallActivityAsync<bool>("DeletionOrchestrator_ExportTemplate", data);
                if (!exportRequest && _options.RequireExport)
                {
                    _log.LogWarning($"Export failed for {data.ResourceGroupName} but export is required.");
                    return;
                }
            }

            if (_options.NotifyOnDeletion)
            {
                await context.CallActivityAsync<DeleteResourceGroupRequest>("DeletionOrchestrator_NotifyOnDelete", data);
            }

            var deletionRequest = await context.CallActivityAsync<DeleteResourceGroupRequest>("DeletionOrchestrator_DeleteResourceGroup", data);
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
                    new BlobAttribute($"templates/{DateTime.UtcNow.ToString("yyyy-MM-dd")}/{request.ResourceGroupName}.json"),
                    new StorageAccountAttribute("ExportTemplateBlobStorageConnection")
               };

            using (var writer = await binder.BindAsync<TextWriter>(attributes).ConfigureAwait(false))
            {
                writer.Write(exportResult.TemplateJson);
            }

            return true;
        }

        [FunctionName("DeletionOrchestrator_NotifyOnDelete")]
        public async Task NotifyOnDelete([ActivityTrigger]DeleteResourceGroupRequest request)
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
        }

        private string GetMailForPrincipal(string principalId)
        {
            try
            {
                Console.ForegroundColor = ConsoleColor.Green;
                var user = _azr.AccessManagement.ActiveDirectoryUsers.GetById(principalId);
                return user.Mail;
            }
            catch (GraphErrorException ex)
            {
                // if not found, search for SP and groups? not sure, since we wouldn't notify a service principal
                if (ex.Response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    try
                    {
                        var g = _azr.AccessManagement.ActiveDirectoryGroups.GetById(principalId);
                        return g.Mail;
                    }
                    catch
                    {
                        // toss, since it's an SP and we don't care about SPs
                        _log.LogError(ex, ex.Message);
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
        }

        [FunctionName("DeletionOrchestrator_Mailer")]
        public void SendMail([ActivityTrigger]List<string> mail)
        {

        }

        [FunctionName("DeletionOrchestrator_DeleteResourceGroup")]
        public void DeleteResourceGroup([ActivityTrigger]DeleteResourceGroupRequest request)
        {
            var azr = _authenticatedStub.WithSubscription(request.SubscriptionId);

            if (_azr == null || _azr.SubscriptionId != request.SubscriptionId)
            {
                _log.LogInformation($"Created a new instance of _azr for subscription {request.SubscriptionId}");
                _azr = _authenticatedStub.WithSubscription(request.SubscriptionId);
            }

            _log.LogInformation($"Deleting resource group {request.ResourceGroupName}");
            _log.LogInformation($"Commit: {_options.Commit}");

            if (_options.Commit)
            {
                _azr.ResourceGroups.BeginDeleteByName(request.ResourceGroupName);
            }
        }
    }
}