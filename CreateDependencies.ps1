# az login
# az account set -s {subscription}
$resourceGroupName = "cosmosdb-scale-test"
$location = "centralus"
$cosmosDBAccountName = "cosmosdb-scale-test-cosmosdb"
$cosmosDBName = "CosmosDbScalingTest"
$cosmosDBContainerName = "scalingdata"

az group create -g $resourceGroupName -l $location
az cosmosdb create -n $cosmosDBAccountName -g $resourceGroupName
az cosmosdb database create -n $cosmosDBAccountName --db-name $cosmosDBName -g $resourceGroupName
az cosmosdb collection create --collection-name $cosmosDBContainerName -g $resourceGroupName --name $cosmosDBAccountName --db-name $cosmosDBName --partition-key-path /partitionkey --throughput 400
$cosmosDBConnectionString = az cosmosdb list-connection-strings -n $cosmosDBAccountName -g $resourceGroupName -o tsv --query "connectionStrings[0].connectionString"
$localsettings = (cat .\CosmosDBTriggerScalingSample\local.settings.json | Convertfrom-json)
$localsettings.Values.CosmosDBConnectionString = $cosmosDBConnectionString

$storageAccountName = "cosmosdbscaleteststorage"
az storage account create -n $storageAccountName -g $resourceGroupName --sku Standard_LRS
$storageAccountConnectionString = az storage account show-connection-string  -n $storageAccountName -g $resourceGroupName --query connectionString -o tsv
$localsettings.Values.StorageConnectionString = $storageAccountConnectionString

$storageTableName = "processingresults"
az storage table create -n $storageTableName --connection-string $storageAccountConnectionString

$storageQueueName = "workqueue"
az storage queue create -n $storageQueueName --connection-string $storageAccountConnectionString

$functionAppName = "cosmosdbscaletestfunctions"
az functionapp create -n $functionAppName -g $resourceGroupName -s $storageAccountName -c $location

$localsettings | ConvertTo-Json >> local.settings.json