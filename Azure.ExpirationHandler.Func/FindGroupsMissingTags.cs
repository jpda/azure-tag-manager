using System;
using System.Threading.Tasks;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Azure.ExpirationHandler.Func
{
    public static class FindGroupsMissingTags
    {
        //[FunctionName("find-groups-missing-tags")]
        //public static void Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, TraceWriter log)
        //{
        //    log.Info($"C# Timer trigger function executed at: {DateTime.Now}");

        //    var config = new ConfigurationBuilder().SetBasePath(context.FunctionAppDirectory).AddJsonFile("local.settings.json", optional: true, reloadOnChange: true).AddEnvironmentVariables().Build();

        //}

        //private async Task GetGroupsWithMissingTags(IAzure azr)
        //{
        //    var groupResponse = await azr.ResourceGroups.ListAsync(true);
        //    var groups = groupResponse.Where(x => x.Tags == null || !x.Tags.ContainsKey(_key)).ToList();
        //    foreach (var g in groups)
        //    {
        //        var expirationDate = DateTime.UtcNow.AddDays(30).ToString("O");
        //        Console.WriteLine($"Tagging {g.Name} with {expirationDate}...");
        //        g.Update().WithTag(_key, expirationDate).Apply();
        //    }
        //    Console.Write($"Found {groups.Count} groups without {_key} tag. ");
        //}
    }
}