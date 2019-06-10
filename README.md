[![Build Status](https://dev.azure.com/jpda/azman/_apis/build/status/jpda.azure-tag-manager?branchName=master)](https://dev.azure.com/jpda/azman/_build/latest?definitionId=14&branchName=master)

# azure-tag-manager :cloud: :zap:

#### :warning: Running locally will use *your user account* which may have more access than your production service principal/MSI.
Timer driven functions run at the top of the hour and iterate through all available subscriptions, so keep in mind that you may end up with resources tagged with an `expires` tag. Just delete the tag if you don't want it, but as of now there is no explicit white or blacklist for subscriptions, it is driven by principal access.

---

Created to help me with resource sprawl. I have a mix of demo, poc, test and production stuff strewn about subscriptions and it's annoying to keep up with...especially when the bill comes :moneybag: I started with Azure Policy, but found I couldn't do evaluations (like created time + 30 days), so decided to build my own tagger.

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fjpda%2Fazure-tag-manager%2Fmaster%2Fazuredeploy.json)

## Current functionality
- [Webhook](/jpda/azure-tag-manager/blob/master/Azure.ExpirationHandler.Func/WebhookResourceGroupCreated.cs) receives notification from Azure Monitor that a new resource group has been created
- Resource group is queued to be tagged by the [Tagger](/jpda/azure-tag-manager/blob/master/Azure.ExpirationHandler.Func/GenerateTagSuite.cs)
- The tagger generates a series of tags for the resource group. Some are automatically added, others are driven by the resource group name.
  - `expires` the default expiration time tag. Defaults to 30 days, can be changed via app setting
  - `owner` the creator of the resource group. Defaults to `unknown` if the principal isn't known, default name configurable via app setting
  - `tagged-by` the system doing the tagging. Defaults to `azman`, default name configurable via app setting
- Hourly, functions run to scan the available subscriptions for both:
  - expired resource groups (e.g., `expires` tag < `DateTime.UtcNow`), which are enqueued for deletion, and
  - resource groups missing tags (driven by `expires` tag not found)
- When a group is enqueued for deletion, you can enable:
  - Notifications, which currently are emails using SendGrid that a resource has been deleted, and
  - Export template, which exports the template via the API to blob storage for archiving


## Name-based tagging
Name-based tagging is driven by the name of the resource group, delimited by dash (-). Right now, position-to-tagname isn't configurable, but it's on the list.

For a sample resource group, named `service-msft-prod-azman-main-compute`:

| Position    | Tag name  | Sample value        | Notes
| ------------|---------  | -------------------:| -----
| 0           | `function`| `service`           | I use these for why I deployed this - `service`, `test`, `demo`, etc
| 1           | `customer`| `msft`              | I do a lot of work for customers so it's a useful tag for me to know who this was for
| 2           | `env`     | `prod`              | Environment, so I can apply certain policy to certain groups
| *remaining* | `thing`   | `azman-main-compute`| Whatever the thing actually is

When tagging is finished, it looks like this:

![sample tags](https://github.com/jpda/azure-tag-manager/raw/master/doc/azman-tag-sample.png "sample tags")

## Resources
todo

## Getting started
todo

## Configuration
Configuration has two parts. First, the Azure Functions configuration, which goes in AppSettings of the function app. There doesn't seem to be support for [Azure App Configuration](https://docs.microsoft.com/en-us/azure/azure-app-configuration/) just yet for storing *all* of the config in App Configuration, so the Function-required settings for bindings are in AppSettings.

Using Azure App Configuration isn't required - as long as these settings are available in AppSettings, environment variables or in App Configuration.
### App Settings configuration
These are mostly related to bindings. You can use different storage accounts for queues or the same one. In most cases a single storage account will be more than sufficient :)

```
"AzureWebJobsStorage": "UseDevelopmentStorage=true",
"FUNCTIONS_WORKER_RUNTIME": "dotnet",
// storage account connection string with the tagging queue
"TaggingQueueStorageAccount": "UseDevelopmentStorage=true", 
// name of the tagging queue
"TaggingQueueName": "generate-tag-suite", 
// storage account connection string with the deletion queue
"DeleteQueueStorageAccount": "UseDevelopmentStorage=true", 
// name of the deletion queue
"DeleteResourceGroupQueueName": "delete-resource-group", 
// storage account connection string with the outbox queue
"OutboxQueueStorageAccount": "UseDevelopmentStorage=true", 
// name of the outbox queue
"OutboxQueueName": "mailer-outbox", 
// sendgrid key for sending mail
"SendGridKey": ""
// App Configuration connection string
"AZMAN-AAC-CONNECTION": "AAC connection string"
```

### Azure App Configuration settings:
These are related to behavior configuration and not tied to Azure. As this gets refactored out more it will be make more sense to have them split.

```
  "TaggingOptions": {
    "ExpirationTagKey": "expires", // tag name to set for expiration
    "ExpirationWindowInDays": 30,
    "DefaultOwner": "unknown",
    "DefaultTaggedByName": "azman"
  },
  "DeletionOptions": {
    "Commit": false, // sort of like a 'what-if' switch, does everything except actually delete the resource group
    "NotifyOnDeletion": false, // enables notifications
    "ExportTemplateBeforeDeletion": true, // enables template export before deletion
    "PersistenceStorageContainer": "templates", // container name for storing exported templates
    "RequireExport": true // requires successful export for deletion to proceed. if `true` and the export fails, the deletion will not happen
  }
```

## Notifications
todo

## Template export
todo

## Deployment
todo

## MSI & authentication considerations
todo