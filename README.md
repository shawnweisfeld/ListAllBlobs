# ListAllBlobs
simple example of listing all the blobs in a container and writing it out to a json file.

Build Status
[![Build Status](https://shawnweisfeld.visualstudio.com/GitHubPipelines/_apis/build/status/GitHubPipelines-ASP.NET%20Core-CI?branchName=master)](https://shawnweisfeld.visualstudio.com/GitHubPipelines/_build/latest?definitionId=14&branchName=master)

Build posted to [Docker Hub](https://hub.docker.com/r/sweisfel/listallblobs)

Example command to pull the docker hub image and run it using Azure Container Instance
``` bash
az container create \
    --name "listallblobs" \
    --resource-group "open-images-dataset" \
    --location southcentralus \
    --cpu 2 \
    --memory 4 \
    --image "sweisfel/listallblobs:latest" \
    --restart-policy Never \
    --environment-variables \
        ListAllblobsSvc__BlobStorageToScanConnectionString="DefaultEndpointsProtocol=http;AccountName=youracct;AccountKey=yourkey;EndpointSuffix=core.windows.net" \
        ListAllblobsSvc__BlobStroageToScanContainer="myblobs" \
        ListAllblobsSvc__BlobStroageToUploadConnectionString="DefaultEndpointsProtocol=http;AccountName=youraccct;AccountKey=yourkey;EndpointSuffix=core.windows.net" \
        ListAllblobsSvc__BlobStroageToUploadContainer="mycounts"
```
[More info on deploying to Azure Container Instance using the Azure CLI](https://docs.microsoft.com/en-us/azure/container-instances/container-instances-quickstart)

