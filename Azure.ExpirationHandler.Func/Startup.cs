using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Rest;
using System;
using static Microsoft.Azure.Management.Fluent.Azure;

namespace Azure.ExpirationHandler.Func
{
    public class Startup : FunctionsStartup
    {
        public Startup() { }

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var config = new ConfigurationBuilder().SetBasePath(Environment.CurrentDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

            builder.Services.AddLogging();
            builder.Services.Configure<TaggingOptions>(config.GetSection("TaggingOptions"));
            builder.Services.Configure<DeletionOptions>(config.GetSection("DeletionOptions"));

            builder.Services.AddSingleton<IAuthenticated>(x =>
            {
                AzureCredentials creds;
                if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSI_ENDPOINT"))) // assume local, should probably be smarter about this
                {
                    creds = new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud);
                }
                else
                {
                    //this is so lame
                    var tenantId = config["TenantId"];
                    var tokenProvider = new AzureServiceTokenProvider();
                    var armToken = tokenProvider.GetAccessTokenAsync("https://management.azure.com", tenantId).Result;
                    var graphToken = tokenProvider.GetAccessTokenAsync("https://graph.microsoft.com", tenantId).Result;
                    creds = new AzureCredentials(new TokenCredentials(armToken), new TokenCredentials(graphToken), tenantId, AzureEnvironment.AzureGlobalCloud);
                }
                return Microsoft.Azure.Management.Fluent.Azure.Configure().Authenticate(creds);
            });
        }
    }
}
