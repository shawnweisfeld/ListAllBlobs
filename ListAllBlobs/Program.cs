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
            Console.WriteLine("Starting");
            long cnt = 0;
            var time = new Stopwatch();

            string connectionString = "";
            string containerName = "";
            var prefixes = new List<string>();

            prefixes.Add("");

            time.Start();

            Parallel.ForEach(prefixes, prefix =>
            {
                string path = $"{prefix}-{containerName}.json";

                if (File.Exists(path))
                    File.Delete(path);

                BlobContainerClient container = new BlobContainerClient(connectionString, containerName);

                using (StreamWriter sw = new StreamWriter(path))
                using (JsonTextWriter jtw = new JsonTextWriter(sw))
                {
                    jtw.Formatting = Formatting.None;

                    jtw.WriteStartArray();

                    foreach (var item in container.GetBlobs(prefix: prefix))
                    {
                        jtw.WriteStartObject();

                        jtw.WritePropertyName("Name");
                        jtw.WriteValue(item.Name);

                        jtw.WritePropertyName("Content-Length");
                        jtw.WriteValue(item.Properties.ContentLength);

                        jtw.WriteEndObject();

                        cnt++;

                        if (cnt % 5000 == 0 && time.Elapsed.TotalSeconds > 0)
                        {
                            Console.WriteLine($"{cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
                        }
                    }

                    jtw.WriteEndArray();
                }
            });

            time.Stop();

            Console.WriteLine($"Done with {cnt:N0} Records in {time.Elapsed.TotalSeconds:N0} seconds ({cnt / time.Elapsed.TotalSeconds:N0} records per second)");
            Console.ReadKey();
        }


    }
}
