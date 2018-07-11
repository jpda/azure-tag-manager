using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Azure.Management.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent;
using Microsoft.Azure.Management.ResourceManager.Fluent.Authentication;
using Microsoft.Azure.Services.AppAuthentication;
//using Microsoft.Rest;
using Newtonsoft.Json;
using Microsoft.Rest;

namespace ExpirationHandler.ConsoleTest
{
    class Program
    {
      

        static void Main(string[] args)
        {
            Console.WriteLine($"Connecting with tenant {Tenant}...");
            var provider = new AzureServiceTokenProvider();
            var accessToken = provider.GetAccessTokenAsync("https://management.azure.com/", Tenant).Result;
            Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud));
            var t = new TokenCredentials(accessToken);
            var a = Azure.Configure().Authenticate(new AzureCredentials(t, t, Tenant, AzureEnvironment.AzureGlobalCloud)).WithSubscription(Subscription);
            Console.WriteLine($"Connected to {a.SubscriptionId}, searching...");
            var c = new Cleaner(a);
            //c.DeleteExpiredGroups(true).Wait();
            c.GetUntaggedResourceGroups().Wait();
            Console.WriteLine("All finished");
            Console.ReadLine();
        }
    }

    public class Cleaner
    {
        IAzure _azr;
        string _key;
        List<IResourceGroup> _groups;

        public Cleaner(IAzure azr, string tagKey = "expires")
        {
            _azr = azr;
            _key = tagKey;
            _groups = _azr.ResourceGroups.ListAsync(true).Result.ToList();
        }

        public void Clean()
        {
            var groups = GetExpirableGroups().Result;
        }

        public async Task GetUntaggedResourceGroups()
        {
            var groupResponse = await _azr.ResourceGroups.ListAsync(true);
            var groups = groupResponse.Where(x => x.Tags == null || !x.Tags.ContainsKey(_key)).ToList();
            foreach (var g in groups)
            {
                var expirationDate = DateTime.UtcNow.AddDays(30).ToString("O");
                Console.WriteLine($"Tagging {g.Name} with {expirationDate}...");
                g.Update().WithTag(_key, expirationDate).Apply();
            }
            Console.Write($"Found {groups.Count} groups without {_key} tag. ");
        }

        public async Task<List<IResourceGroup>> GetExpirableGroups(string expirationTagKey = "expires")
        {
            var groupResponse = await _azr.ResourceGroups.ListAsync(true);
            var groups = groupResponse.Where(x => x.Tags != null && x.Tags.ContainsKey(_key)).ToList();
            Console.Write($"Found {groups.Count} groups. ");
            var groupsThatExpire = groups.Where(x => x.Tags != null && x.Tags.ContainsKey(_key));
            Console.WriteLine($"{groupsThatExpire.Count()} are expirable.");
            return groups;
        }

        public async Task AddTagSuite()
        {
            foreach (var g in _groups)
            {
                Dictionary<string, string> tags;
                if (g.Tags != null)
                {
                    tags = new Dictionary<string, string>(g.Tags);
                }
                else
                {
                    tags = new Dictionary<string, string>();
                }
                var pieces = g.Name.Split('-');
                if (pieces.Length >= 4)
                {
                    Console.WriteLine($"Working on {g.Name}");
                    var function = pieces[0];
                    var customer = pieces[1];
                    var env = pieces[2];
                    var name = string.Join('-', pieces.Skip(3));

                    tags.Add("function", function);
                    tags.Add("customer", customer);
                    tags.Add("env", env);
                    tags.Add("thing", name);
                };

                tags.Add("expires", DateTime.Now.AddDays(30).ToString("o"));
                foreach (var a in tags)
                {
                    Console.WriteLine($"{a.Key}: {a.Value}");
                }

                g.Update().WithTags(tags).Apply();
            }
        }

        public async Task DeleteExpiredGroups(bool commit = false)
        {
            var def = Console.ForegroundColor;
            var groupResponse = await _azr.ResourceGroups.ListAsync(true);
            var groups = groupResponse.Where(x => x.Tags != null && x.Tags.ContainsKey(_key) && DateTime.Parse(x.Tags[_key]) < DateTime.UtcNow).ToList();
            foreach (var g in groups)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Group {g.Name} expired on {DateTime.Parse(g.Tags[_key])}. Deleting...");
                if (commit)
                {
                    _azr.ResourceGroups.BeginDeleteByName(g.Name);
                }
                Console.WriteLine("done.");
            }
            Console.ForegroundColor = def;
            Console.Write($"Deleted {groups.Count} groups without {_key} tag. ");
        }

        public async Task FindExpired(List<IResourceGroup> groupsThatExpire)
        {

            var def = Console.ForegroundColor;

            foreach (var g in groupsThatExpire)
            {
                var expirationDate = DateTime.Parse(g.Tags[_key]);
                if (expirationDate > DateTime.UtcNow.AddDays(7))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                }
                if (expirationDate < DateTime.UtcNow.AddDays(7))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                }
                if (expirationDate < DateTime.UtcNow)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                }

                Console.WriteLine($"{g.Name} expires on {g.Tags[_key]} ({DateTime.Parse(g.Tags[_key])})");
                Console.ForegroundColor = def;
            }

            var expiredGroups = groupsThatExpire.Where(x => DateTime.Parse(x.Tags[_key]) < DateTime.UtcNow);

            //.Select(y => y.Tags.Where(z => DateTime.Parse(z.Value) < DateTime.UtcNow));
            //foreach (var g in groupsThatExpire)
            //{
            //    Console.WriteLine($"{g.Name}, {g.Tags[expirationTagKey]}");
            //}
        }

        public async Task ParseWebhook()
        {
            var jsonContent = await System.IO.File.ReadAllTextAsync(@"..\..\..\sample-webhook.json");
            dynamic root = JsonConvert.DeserializeObject(jsonContent);
            Console.WriteLine($"found {root.data.context.activityLog.operationName} for {root.data.context.activityLog.resourceGroupName} by {root.data.context.activityLog.caller}");

        }
    }

}
