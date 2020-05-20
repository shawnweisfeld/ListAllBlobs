using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ListAllBlobs
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Starting");
            long cnt = 0;
            var time = new Stopwatch();

            string connectionString = "your connection string";
            string containerName = "your container name";
            string path = $"{containerName}.json";

            if (File.Exists(path))
                File.Delete(path);

            time.Start();

            BlobContainerClient container = new BlobContainerClient(connectionString, containerName);

            using (StreamWriter sw = new StreamWriter(path))
            using (JsonTextWriter jtw = new JsonTextWriter(sw))
            {
                jtw.Formatting = Formatting.None;

                jtw.WriteStartArray();

                await foreach (var item in container.GetBlobsAsync())
                {
                    jtw.WriteStartObject();

                    jtw.WritePropertyName("Name");
                    jtw.WriteValue(item.Name);

                    jtw.WritePropertyName("Content-Length");
                    jtw.WriteValue(item.Properties.ContentLength);

                    jtw.WriteEndObject();

                    cnt++;

                    if (cnt % 5000 == 0)
                    {
                        Console.WriteLine($"{cnt:N0} Records in {time.Elapsed.Seconds:N0} seconds ({cnt/time.Elapsed.Seconds:N0} records per second)");
                    }
                }

                jtw.WriteEndArray();
            }

            time.Stop();

            Console.WriteLine($"Done with {cnt:N0} Records in {time.Elapsed.Seconds:N0} seconds ({cnt / time.Elapsed.Seconds:N0} records per second)");
            Console.ReadKey();
        }


    }
}
