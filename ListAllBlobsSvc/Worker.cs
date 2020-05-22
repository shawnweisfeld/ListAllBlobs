using System;
using System.Buffers.Text;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ListAllBlobsSvc
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        private readonly AppConfig _config;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;

        public Worker(ILogger<Worker> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<AppConfig> options)
        {
            _logger = logger;
            _config = options.Value;
            _hostApplicationLifetime = hostApplicationLifetime;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Worker Cancelling");
            });

            try
            {
                await ExecuteContainer(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Operation Canceled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled Exception");
            }
            finally
            {
                _hostApplicationLifetime.StopApplication();
            }
        }


        private async Task ExecuteContainer(CancellationToken stoppingToken)
        {
            //keep count of how long we have been working
            var time = new Stopwatch();

            //tell the user we are starting and start the clock
            _logger.LogDebug("Starting");
            time.Start();

            //a list of all the results from scanning our container
            var prefixTasks = new List<Task<ContainerPrefixScanResult>>();

            //Get a client to connect to the blob container
            BlobContainerClient toScanContainer = new BlobContainerClient(_config.BlobStorageToScanConnectionString, _config.BlobStroageToScanContainer);

            //iterate over all the prefixs (folders) in the root container
            //NOTE: we are ignoring any blobs in the root conatiner
            await foreach (var item in toScanContainer.GetBlobsByHierarchyAsync(delimiter: "/", cancellationToken: stoppingToken))
            {
                if (item.IsPrefix)
                {
                    _logger.LogDebug($"Found prefix {item.Prefix}");
                    prefixTasks.Add(ExecuteContainerPrefixAsync(item.Prefix.TrimEnd('/'), stoppingToken));
                }
            }

            //wait for the scan of each of the prefixes to finish
            var results = await Task.WhenAll(prefixTasks);

            time.Stop();

            //tell the user we are done
            var ttlBlobs = results.Sum(x => x.TotalBlobs);
            var ttlTime = time.Elapsed.TotalSeconds;
            _logger.LogInformation($"ALL Done with {ttlBlobs:N0} Records in {ttlTime:N0} seconds ({ttlBlobs / ttlTime:N0} records per second)");
        }


        private async Task<ContainerPrefixScanResult> ExecuteContainerPrefixAsync(string prefix, CancellationToken stoppingToken)
        {
            string file = $"{_config.BlobStroageToScanContainer}-{prefix}.json";

            //keep count of how many blobs we have seen
            long cnt = 0;

            //keep count of how long we have been working
            var time = new Stopwatch();

            //tell the user we are starting and start the clock
            _logger.LogDebug($"Starting {_config.BlobStroageToScanContainer}-{prefix}");
            time.Start();

            //Get a client to connect to the blob container
            BlobContainerClient toScanContainer = new BlobContainerClient(_config.BlobStorageToScanConnectionString, _config.BlobStroageToScanContainer);

            //NOTE: using JsonTextWriter vs the seralizer to improve write performance
            using (StreamWriter sw = new StreamWriter(file))
            using (JsonTextWriter jtw = new JsonTextWriter(sw))
            {
                //turn off formatting in the json file to save space
                jtw.Formatting = Formatting.None;

                //write each blob item as an element in an array
                jtw.WriteStartArray();

                //iterate over all the blobs we found
                await foreach (var item in toScanContainer.GetBlobsAsync(prefix: prefix, cancellationToken: stoppingToken))
                {
                    //write the details of the blob as a json object to the array
                    jtw.WriteStartObject();

                    jtw.WritePropertyName("Name");
                    jtw.WriteValue(item.Name);

                    jtw.WritePropertyName("Content-Length");
                    jtw.WriteValue(item.Properties.ContentLength);

                    jtw.WritePropertyName("Content-MD5");
                    jtw.WriteValue(Convert.ToBase64String(item.Properties.ContentHash));

                    jtw.WriteEndObject();

                    //increment the counter
                    cnt++;

                    //provide some feedback to the user that something is happening
                    if (cnt % 5000 == 0 && time.Elapsed.TotalSeconds > 0)
                    {
                        _logger.LogDebug($"{_config.BlobStroageToScanContainer}-{prefix}: {cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
                    }
                }

                //finalize the json doc
                jtw.WriteEndArray();

                _logger.LogInformation($"{_config.BlobStroageToScanContainer}-{prefix} Done with {cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
            }

            try
            {
                var toUploadContainer = new BlobContainerClient(_config.BlobStroageToUploadConnectionString, _config.BlobStroageToUploadContainer);
                var toUploadBob = toUploadContainer.GetBlobClient(file);

                //if the file already exists on the server delete it
                await toUploadBob.DeleteIfExistsAsync(cancellationToken: stoppingToken);

                //upload the file
                await toUploadBob.UploadAsync(file, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error on Upload {file}");
                throw;
            }

            time.Stop();

            return new ContainerPrefixScanResult()
            {
                TotalBlobs = cnt,
                TotalSeconds = time.Elapsed.TotalSeconds
            };
        }

    }
}
