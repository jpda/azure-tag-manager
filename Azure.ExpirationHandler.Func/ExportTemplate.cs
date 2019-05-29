using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.IO;
using static Microsoft.Azure.Management.Fluent.Azure;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Azure.ExpirationHandler.Func;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public class ExportTemplate : AzureAuthenticatedBase
    {
        public ExportTemplate(IAuthenticated auth, ILoggerFactory loggerFactory) : base(auth, loggerFactory) { }

        [FunctionName("export-template")]
        public async Task Run([QueueTrigger("export-template", Connection = "ExportTemplateStorageQueueConnection")]string exportQueueItem, Binder binder)
        {
            dynamic data = JsonConvert.DeserializeObject(exportQueueItem);
            string rgName = data.ResourceGroupName.ToString();
            string subscriptionId = data.SubscriptionId.ToString();

            var azr = _authenticatedStub.WithSubscription(subscriptionId);
            var exportResult = await azr.ResourceGroups.GetByName(rgName).ExportTemplateAsync(ResourceGroupExportTemplateOptions.IncludeBoth);

            if (exportResult.Error != null)
            {
                _log.LogError($"ExportTemplate error: {exportResult.Error.Code} {exportResult.Error.Message} at {exportResult.Error.Target}");
            }

            var attributes = new Attribute[]
               {
                    new BlobAttribute($"templates/{DateTime.UtcNow.ToString("yyyy-MM-dd")}/{rgName}.json"),
                    new StorageAccountAttribute("ExportTemplateBlobStorageConnection")
               };

            using (var writer = await binder.BindAsync<TextWriter>(attributes).ConfigureAwait(false))
            {
                writer.Write(exportResult.TemplateJson);
            }
        }
    }
}