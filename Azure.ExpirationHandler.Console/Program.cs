﻿using System;
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
using static Microsoft.Azure.Management.Fluent.Azure;
using System.Runtime.InteropServices.ComTypes;
using Microsoft.Azure.Management.Graph.RBAC.Fluent.Models;

namespace ExpirationHandler.ConsoleTest
{
    class Program
    {
        private const string Tenant = "72f988bf-86f1-41af-91ab-2d7cd011db47";
        private const string Subscription = "e7048bdb-835c-440f-9304-aa4171382839";

        static void Main(string[] args)
        {
            Console.WriteLine($"Connecting with tenant {Tenant}...");
            var provider = new AzureServiceTokenProvider();
            var accessToken = provider.GetAccessTokenAsync("https://management.azure.com/", Tenant).Result;
            var graphToken = provider.GetAccessTokenAsync("https://graph.windows.net/", Tenant).Result;
            //Azure.Configure().Authenticate(new AzureCredentials(new MSILoginInformation(MSIResourceType.AppService), AzureEnvironment.AzureGlobalCloud));
            var t = new TokenCredentials(accessToken);
            var gt = new TokenCredentials(graphToken);

            var az = Azure.Configure().Authenticate(new AzureCredentials(t, gt, Tenant, AzureEnvironment.AzureGlobalCloud));

            //.WithSubscription(Subscription);
            //foreach (var sub in az.Subscriptions.ListAsync().Result)
            //{
            //var a = az.WithSubscription(sub.SubscriptionId);
            //Console.WriteLine($"Connected to {sub.SubscriptionId}, searching...");
            //var c = new Cleaner(a);
            //c.DeleteExpiredGroups(true).Wait();
            //c.GetUntaggedResourceGroups().Wait();
            //}
            //var a = az.WithSubscription(Subscription);
            Console.WriteLine($"Connected to {Subscription}, searching...");
            var c = new Cleaner(az);
            c.Assignments();

            Console.WriteLine("All finished");
            Console.ReadLine();
        }
    }

    public class Cleaner
    {
        IAuthenticated _azAuth;
        IAzure _azr;
        string _key;
        List<IResourceGroup> _groups;

        public Cleaner(IAzure azr, string tagKey = "expires")
        {
            _azr = azr;
            _key = tagKey;
            //_groups = _azr.ResourceGroups.ListAsync(true).Result.ToList();
        }

        public Cleaner(IAuthenticated azAuth) : this(azAuth.WithSubscription("e7048bdb-835c-440f-9304-aa4171382839"))
        {
            _azAuth = azAuth;
        }

        public void Assignments()
        {
            var rg = _azr.ResourceGroups.GetByName("demo-me-p-brutus-app-modernization");
            var o = _azAuth.RoleDefinitions.GetByScopeAndRoleName(rg.Id, "Owner");

            var owners = _azAuth.RoleAssignments.ListByScope(rg.Id).Where(x => x.RoleDefinitionId == o.Id);
            var owners2 = _azAuth.RoleAssignments.ListByScope(rg.Id);
            Console.WriteLine($"found {owners.Count()} in role list, {owners2.Count()} without filter");
            string name = "";

            foreach (var a in owners)
            {
                Console.WriteLine($"{a.Id}: {a.Name}: {a.PrincipalId}");


                var rolename = _azr.AccessManagement.RoleDefinitions.GetById(a.RoleDefinitionId).RoleName;

                Console.WriteLine($"{rolename}: {name}");
                Console.ResetColor();
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
                    catch (GraphErrorException gex)
                    {
                        // toss, since it's an SP and we don't care about SPs
                        return string.Empty;
                    }
                }
                return string.Empty;
            }
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

        public void AddTagSuite()
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

        public void FindExpired(List<IResourceGroup> groupsThatExpire)
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
