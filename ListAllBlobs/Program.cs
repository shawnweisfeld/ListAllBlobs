using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ListAllBlobs
{
    class Program
    {
        static void Main(string[] args)
        {
            //keep count of how many blobs we have seen
            long cnt = 0;

            //keep count of how long we have been working
            var time = new Stopwatch();

            //fill this in with the connection string to your Azure storage account
            string connectionString = "";

            //fill this in with the container name in your Azure storage account
            string containerName = "";

            //a list of all the prefixes (folders) in Azure storage you want to scan
            //we will put each prefix on its own thread
            var prefixes = new List<string>();

            BlobContainerClient bcc = new BlobContainerClient(connectionString, containerName);

            foreach (var item in bcc.GetBlobsByHierarchy(delimiter: "/"))
            {
                if (item.IsPrefix)
                {
                    Console.WriteLine($"Found prefix {item.Prefix}");
                    prefixes.Add(item.Prefix.TrimEnd('/'));
                }
            }

            //tell the user we are starting and start the clock
            Console.WriteLine("Starting");
            time.Start();

            //iterate over each of the prefixes we found
            Parallel.ForEach(prefixes, prefix =>
            {
                //name of jason file to store the results
                string path = $"{prefix}-{containerName}.json";

                //if it exists delete it first
                if (File.Exists(path))
                    File.Delete(path);

                //open the file and start writing the results
                //NOTE: using JsonTextWriter vs the seralizer to improve write performance
                using (StreamWriter sw = new StreamWriter(path))
                using (JsonTextWriter jtw = new JsonTextWriter(sw))
                {
                    //turn off formatting in the json file to save space
                    jtw.Formatting = Formatting.None;

                    //write each blob item as an element in an array
                    jtw.WriteStartArray();

                    //iterate over all the blobs we found
                    foreach (var item in bcc.GetBlobs(prefix: prefix))
                    {
                        //write the details of the blob as a json object to the array
                        jtw.WriteStartObject();

                        jtw.WritePropertyName("Name");
                        jtw.WriteValue(item.Name);

                        jtw.WritePropertyName("Content-Length");
                        jtw.WriteValue(item.Properties.ContentLength);

                        jtw.WriteEndObject();

                        //increment the counter
                        cnt++;

                        //provide some feedback to the user that something is happening
                        if (cnt % 5000 == 0 && time.Elapsed.TotalSeconds > 0)
                        {
                            Console.WriteLine($"{cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
                        }
                    }

                    //finalize the file
                    jtw.WriteEndArray();
                }
            });

            time.Stop();

            //tell the user we are done, and wait for them to press a key to exit
            Console.WriteLine($"Done with {cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
            Console.WriteLine("Press any key to exit");
            Console.ReadKey();
        }


    }
}
