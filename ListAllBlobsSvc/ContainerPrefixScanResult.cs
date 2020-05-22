using System;
using System.Collections.Generic;
using System.Text;

namespace ListAllBlobsSvc
{
    public class ContainerPrefixScanResult
    {
        public double TotalSeconds { get; set; }
        public long TotalBlobs { get; set; }
    }
}
