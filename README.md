[![Build Status](https://dev.azure.com/jpda/azman/_apis/build/status/jpda.azure-tag-manager?branchName=master)](https://dev.azure.com/jpda/azman/_build/latest?definitionId=14&branchName=master)
# azure-tag-manager

Created to help me with resource sprawl. I have a mix of demo, poc, test and production stuff strewn about subscriptions and it's annoying to keep up with. Especially when the bill comes :/

[![Deploy to Azure](http://azuredeploy.net/deploybutton.png)](https://portal.azure.com/#create/Microsoft.Template/uri/https%3A%2F%2Fraw.githubusercontent.com%2Fjpda%2Fazure-tag-manager%2Fmaster%2Fazuredeploy.json)  

## Adds tags based on resource group naming nomenclature
- Driven by Azure Monitor: whenever a new resource group is created, a webhook calls the webhook-rg-created function, which parses the hook and queues up a message to the tagger.
- Expiration tag is added to default to now + 30 days. 
- Tagger splits name and creates tags based on positional parameters (this could probably be better)
