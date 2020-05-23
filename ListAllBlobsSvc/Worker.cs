using System;
using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace ListAllBlobsSvc
{
    public class Worker : BackgroundService
    {
        private const string DELIMITER = "/";
        private readonly ILogger<Worker> _logger;
        private readonly AppConfig _config;
        private readonly IHostApplicationLifetime _hostApplicationLifetime;
        private readonly SemaphoreSlim _slim;
        private readonly ConcurrentBag<Task> _todo;
        private long _ttlBlobs;
        private long _ttlFolders;

        /// <summary>
        /// Set Everything up
        /// </summary>
        /// <param name="logger">.NET Core Logger</param>
        /// <param name="hostApplicationLifetime">Listen for Application events, like close</param>
        /// <param name="options">Configuration Information</param>
        public Worker(ILogger<Worker> logger,
            IHostApplicationLifetime hostApplicationLifetime,
            IOptions<AppConfig> options)
        {
            _logger = logger;
            _config = options.Value;
            _hostApplicationLifetime = hostApplicationLifetime;
            _todo = new ConcurrentBag<Task>();
            _ttlBlobs = 0;
            _ttlFolders = 0;

            //Set the starting point to the root if not provided
            if (string.IsNullOrEmpty(_config.BlobStroageToScanPrefix))
            {
                _config.BlobStroageToScanPrefix = string.Empty;
            }
            //If starting point is provide, ensure that it has the slash at the end
            else if (!_config.BlobStroageToScanPrefix.EndsWith(DELIMITER))
            {
                _config.BlobStroageToScanPrefix = _config.BlobStroageToScanPrefix + DELIMITER;
            }

            //Set the default thread count if one was not set
            if (_config.ThreadCount < 1)
            {
                _config.ThreadCount = Environment.ProcessorCount * 2;
            }

            _logger.LogInformation($"Using {_config.ThreadCount} threads");

            //The Semaphore ensures how many scans can happen at the same time
            _slim = new SemaphoreSlim(_config.ThreadCount);
        }


        /// <summary>
        /// Main Entry point
        /// </summary>
        /// <param name="stoppingToken">Someone is trying to turn us off</param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Notify user that we were asked to shutdown
            stoppingToken.Register(() =>
            {
                _logger.LogInformation("Worker Cancelling");
            });

            try
            {
                //keep count of how much work we did
                var time = new Stopwatch();

                //tell the user we are starting and start the clock
                _logger.LogDebug("Starting");
                time.Start();

                //Start the processing with the root folder
                ProcessFolder(_config.BlobStroageToScanPrefix, stoppingToken);

                //wait for enough to get the todo list so we don't exit before we started
                await Task.Delay(1000);

                // wait while there are any tasks that have not finished
                while (_todo.Any(x => !x.IsCompleted))
                {
                    _logger.LogDebug($"Waiting.");
                    await Task.Delay(1000);
                }

                time.Stop();

                //tell the user we are done
                var ttlTime = time.Elapsed.TotalSeconds;
                _logger.LogInformation($"ALL Done with {_ttlBlobs:N0} blobs in {_ttlFolders:N0} folders in {ttlTime:N0} seconds ({_ttlBlobs / ttlTime:N0} blobs per second)");
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

        private void ProcessFolder(string prefix, CancellationToken stoppingToken)
        {
            //Create a new task to process the folder
            _todo.Add(Task.Run(async () =>
            {
                //wait till there is a open thread to work on
                _slim.Wait();
                
                try
                {
                    var time = new Stopwatch();
                    time.Start();
                    var blobCount = 0;

                    //Get a client to connect to the blob container
                    BlobContainerClient toScanContainer = new BlobContainerClient(_config.BlobStorageToScanConnectionString, _config.BlobStroageToScanContainer);

                    string file = $"{_config.BlobStroageToScanContainer}-{prefix.Replace(DELIMITER, "-")}.json";

                    _logger.LogDebug($"Searching Prefix '{prefix}'");

                    //NOTE: using JsonTextWriter vs the seralizer to improve write performance
                    using (StreamWriter sw = new StreamWriter(file))
                    using (JsonTextWriter jtw = new JsonTextWriter(sw))
                    {
                        //turn off formatting in the json file to save space
                        jtw.Formatting = Formatting.None;

                        //write each blob item as an element in an array
                        jtw.WriteStartArray();

                        //iterate over all the items we find
                        await foreach (var item in toScanContainer.GetBlobsByHierarchyAsync(prefix: prefix, delimiter: DELIMITER, cancellationToken: stoppingToken))
                        {
                            //I found another folder, recurse
                            if (item.IsPrefix)
                            {
                                ProcessFolder(item.Prefix, stoppingToken);
                            }
                            //I found a file, write it out to the json file
                            else if (item.IsBlob)
                            {
                                //write the details of the blob as a json object to the array
                                jtw.WriteStartObject();

                                jtw.WritePropertyName("Name");
                                jtw.WriteValue(item.Blob.Name);

                                jtw.WritePropertyName("Content-Length");
                                jtw.WriteValue(item.Blob.Properties.ContentLength);

                                jtw.WritePropertyName("Content-MD5");
                                jtw.WriteValue(Convert.ToBase64String(item.Blob.Properties.ContentHash));

                                jtw.WritePropertyName("Last-Modified");
                                jtw.WriteValue(item.Blob.Properties.LastModified);

                                jtw.WriteEndObject();

                                //increment the counter
                                blobCount++;
                            }
                        }

                        //finalize the json doc
                        jtw.WriteEndArray();
                    }

                    //Get a reference to the storage account we are to put the json files once we created them
                    var toUploadContainer = new BlobContainerClient(_config.BlobStroageToUploadConnectionString, _config.BlobStroageToUploadContainer);
                    var toUploadBob = toUploadContainer.GetBlobClient(file);

                    //if the file already exists on the server delete it
                    await toUploadBob.DeleteIfExistsAsync(cancellationToken: stoppingToken);

                    //upload the file
                    await toUploadBob.UploadAsync(file, stoppingToken);

                    //update our counts
                    Interlocked.Add(ref _ttlBlobs, blobCount);
                    Interlocked.Add(ref _ttlFolders, 1);

                    time.Stop();

                    //tell the user we are done
                    var ttlTime = time.Elapsed.TotalSeconds;
                    _logger.LogInformation($"Folder Done with {blobCount:N0} blobs in {ttlTime:N0} seconds ({blobCount / ttlTime:N0} blobs per second)");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
                
                //release our thread in the pool, allowing someone else to get in
                _slim.Release();
            }, stoppingToken));

        }
    }
}
