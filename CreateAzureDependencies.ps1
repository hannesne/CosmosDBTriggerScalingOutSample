# az login
# az account set -s {subscription}
$resourceGroupName = "cosmosdb-scale-test"
$location = "centralus"
$cosmosDBAccountName = "cosmosdb-scale-test-cosmosdb"
$cosmosDBName = "CosmosDbScalingTest"
$cosmosDBContainerName = "scalingdata"
$storageAccountName = "cosmosdbscaleteststorage"
$storageTableName = "processingresults"
$storageQueueName = "workqueue"
$functionAppName = "cosmosdbscaletestfunctions"


az group delete -g $resourceGroupName
# az group create -g $resourceGroupName -l $location

az cosmosdb create -n $cosmosDBAccountName -g $resourceGroupName
az cosmosdb database create -n $cosmosDBAccountName --db-name $cosmosDBName -g $resourceGroupName
az cosmosdb collection create --collection-name $cosmosDBContainerName -g $resourceGroupName --name $cosmosDBAccountName --db-name $cosmosDBName --partition-key-path /partitionkey --throughput 400
$cosmosDBConnectionString = az cosmosdb list-connection-strings -n $cosmosDBAccountName -g $resourceGroupName -o tsv --query "connectionStrings[0].connectionString"

az storage account create -n $storageAccountName -g $resourceGroupName --sku Standard_LRS
$storageAccountConnectionString = az storage account show-connection-string  -n $storageAccountName -g $resourceGroupName --query connectionString -o tsv


az storage table create -n $storageTableName --connection-string $storageAccountConnectionString

az storage queue create -n $storageQueueName --connection-string $storageAccountConnectionString

az functionapp create -n $functionAppName -g $resourceGroupName -s $storageAccountName -c $location

$appInsightsName = "cosmosdb-scale-test-appinsights"

$instrumentationKey = az resource create -n $appInsightsName -g $resourceGroupName --resource-type "Microsoft.Insights/components" -l "EastUS" --properties '{\"Application_Type\":\"web\"}' --query "properties.InstrumentationKey" -o tsv

Write-Host "Instrumentation Key = " $instrumentationKey

$localsettings = (cat .\CosmosDBTriggerScalingSample\local.settings.json | Convertfrom-json)
$localsettings.Values.CosmosDBConnectionString = $cosmosDBConnectionString
$localsettings.Values.StorageConnectionString = $storageAccountConnectionString
$localsettings | ConvertTo-Json > local.settings.json

Write-Host "Generated new localsettings file with new connection strings"
