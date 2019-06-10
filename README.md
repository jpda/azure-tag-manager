[![Build Status](https://dev.azure.com/jpda/azman/_apis/build/status/jpda.azure-tag-manager?branchName=master)](https://dev.azure.com/jpda/azman/_build/latest?definitionId=14&branchName=master)
# azure-tag-manager

Created to help me with resource sprawl. I have a mix of demo, poc, test and production stuff strewn about subscriptions and it's annoying to keep up with. Especially when the bill comes :/ I started with Azure Policy, but found I couldn't do evaluations (like created time + 30 days), so decided to build my own tagger.

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fjpda%2Fazure-tag-manager%2Fmaster%2Fazuredeploy.json)

## Current functionality
- [Webhook](/jpda/azure-tag-manager/blob/master/Azure.ExpirationHandler.Func/WebhookResourceGroupCreated.cs) receives notification from Azure Monitor that a new resource group has been created
- Resource group is queued to be tagged by the [Tagger](/jpda/azure-tag-manager/blob/master/Azure.ExpirationHandler.Func/GenerateTagSuite.cs)
- The tagger generates a series of tags for the resource group. Some are automatically added, others are driven by the resource group name.
  - `expires` the default expiration time tag. Defaults to 30 days, can be changed via app setting
  - `owner` the creator of the resource group. Defaults to `unknown` if the principal isn't known, default name configurable via app setting
  - `tagged-by` the system doing the tagging. Defaults to `azman`, default name configurable via app setting

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

![sample tags](https://github.com/jpda/azure-tag-manager/raw/master/azman-tag-sample.png "sample tags")

## Notifications
todo

## Template export
todo

## Deployment
todo

## MSI & authentication considerations
todo