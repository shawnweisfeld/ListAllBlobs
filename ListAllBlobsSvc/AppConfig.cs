using System;
using System.Collections.Generic;
using System.Text;

namespace ListAllBlobsSvc
{
    /// <summary>
    /// Application Configuration Information
    /// </summary>
    public class AppConfig
    {
        public string BlobStorageToScanConnectionString { get; set; }
        public string BlobStroageToScanContainer { get; set; }
        public string BlobStroageToScanPrefix { get; set; }

        public string BlobStroageToUploadConnectionString { get; set; }
        public string BlobStroageToUploadContainer { get; set; }

        public int ThreadCount { get; set; }
    }
}
