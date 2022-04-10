// -----------------------------------------------------------------------
// <copyright file="Program.NetCore.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Net.Http;
using Newtonsoft.Json.Linq;

namespace Lighthouse
{
    public partial class Program
    {
#if CORECLR
        public static void Main(string[] args)
        {
            string metadataString = string.Empty;
            var ecsEndpoint = Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4");
            using (var httpClient = new HttpClient())
            {
                var response = httpClient.GetAsync(Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4") + "/taskWithTags").Result;
                metadataString = response.Content.ReadAsStringAsync().Result;
                Console.WriteLine("ECS Metadata: " + metadataString);
                var metadata = JObject.Parse(metadataString);
                var taskIdSplit = metadata["TaskARN"].ToString().Split("/");
                var taskId = taskIdSplit[taskIdSplit.Length - 1];
                Environment.SetEnvironmentVariable("CLUSTER_IP", $"{taskId}.lighthouse.batch-import-system");
           }
            var lighthouseService = new LighthouseService();
            lighthouseService.Start();
            Console.WriteLine("Press Control + C to terminate.");
            Console.CancelKeyPress += async (sender, eventArgs) => { await lighthouseService.StopAsync(); };
            lighthouseService.TerminationHandle.Wait();
        }
#endif
    }
}