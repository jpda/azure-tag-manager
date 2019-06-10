using Microsoft.Extensions.Logging;
using static Microsoft.Azure.Management.Fluent.Azure;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Azure.ExpirationHandler.Func;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Azure.ExpirationHandler.Func
{
    public abstract class AzureAuthenticatedBase
    {
        protected readonly IAuthenticated _authenticatedStub;
        protected readonly ILogger _log;

        protected AzureAuthenticatedBase(IAuthenticated auth, ILoggerFactory loggerFactory)
        {
            _authenticatedStub = auth;
            _log = loggerFactory.CreateLogger(this.GetType());
        }
    }
}