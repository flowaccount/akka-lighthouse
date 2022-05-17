// -----------------------------------------------------------------------
// <copyright file="LighthouseHostFactory.cs" company="Petabridge, LLC">
//      Copyright (C) 2015 - 2019 Petabridge, LLC <https://petabridge.com>
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Akka.Actor;
using Akka.Bootstrap.Docker;
using Akka.Configuration;
using Newtonsoft.Json.Linq;
using static System.String;

namespace Lighthouse
{
    /// <summary>
    ///     Launcher for the Lighthouse <see cref="ActorSystem" />
    /// </summary>
    public static class LighthouseHostFactory
    {
        public static ActorSystem LaunchLighthouse(string ipAddress = null, int? specifiedPort = null,
            string systemName = null)
        {
            systemName = systemName ?? Environment.GetEnvironmentVariable("ACTORSYSTEM")?.Trim();
            var publicHostname = Environment.GetEnvironmentVariable("AKKA__CLUSTER__DNS")?.Trim();
            var argConfig = "";
            if (ipAddress != null)
                argConfig += $"akka.remote.dot-netty.tcp.public-hostname = {ipAddress}\n";
            if (specifiedPort != null)
                argConfig += $"akka.remote.dot-netty.tcp.port = {specifiedPort}";

            var useDocker = !(IsNullOrEmpty(Environment.GetEnvironmentVariable("CLUSTER_IP")?.Trim()) ||
                             IsNullOrEmpty(Environment.GetEnvironmentVariable("CLUSTER_SEEDS")?.Trim()));

            var clusterConfig = ConfigurationFactory.ParseString(File.ReadAllText("akka.hocon"));

            // If none of the environment variables expected by Akka.Bootstrap.Docker are set, use only what's in HOCON
            if (useDocker)
                clusterConfig = clusterConfig.BootstrapFromDocker();

            // Use ecs metadata and service discovery
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
                
                publicHostname = string.IsNullOrEmpty(publicHostname) ? "batch-import-system" : publicHostname;

                var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")?.Trim().ToLowerInvariant();
                var subdomainName = !string.IsNullOrEmpty(environment) && environment == "production" ? "lighthouse": $"lighthouse-{environment}";

                var remoteIpConfig = $"akka.remote.dot-netty.tcp.hostname = 0.0.0.0\n";
                clusterConfig = ConfigurationFactory.ParseString(remoteIpConfig)
                    .WithFallback(clusterConfig);
                Console.WriteLine($"setting subdomain {subdomainName}");
                var remoteConfig = $"akka.remote.dot-netty.tcp.public-hostname = {taskId}.{subdomainName}.{publicHostname}\n";
                clusterConfig = ConfigurationFactory.ParseString(remoteConfig)
                    .WithFallback(clusterConfig);

                // var configuredPort = clusterConfig.GetValue("akka.remote.dot-netty.tcp.port").GetInt();
                // var pport = metadata["Containers"][0]["Ports"].Where(portConfig => portConfig["ContainerPort"].ToString() == configuredPort.ToString()).Select(portConfig => portConfig["HostPort"] ).FirstOrDefault().ToString();
                // Console.WriteLine("Setting the public-port variable: " + $"{taskId}.{pport}");
                // var publicPortConfig = $"akka.remote.dot-netty.tcp.public-port = {pport}\n";
                // clusterConfig = ConfigurationFactory.ParseString(publicPortConfig)
                //     .WithFallback(clusterConfig);
           }

            // Values from method arguments should always win
            if (!IsNullOrEmpty(argConfig))
                clusterConfig = ConfigurationFactory.ParseString(argConfig)
                    .WithFallback(clusterConfig);
            
            var lighthouseConfig = clusterConfig.GetConfig("lighthouse");
            if (lighthouseConfig != null && IsNullOrEmpty(systemName))
                systemName = lighthouseConfig.GetString("actorsystem", systemName);

            ipAddress = clusterConfig.GetString("akka.remote.dot-netty.tcp.public-hostname", "127.0.0.1");
            var port = clusterConfig.GetInt("akka.remote.dot-netty.tcp.port");

            var sslEnabled = clusterConfig.GetBoolean("akka.remote.dot-netty.tcp.enable-ssl");
            var selfAddress = sslEnabled ? new Address("akka.ssl.tcp", systemName, ipAddress.Trim(), port).ToString()
                    : new Address("akka.tcp", systemName, ipAddress.Trim(), port).ToString();

            /*
             * Sanity check
             */
            Console.WriteLine($"[Lighthouse] ActorSystem: {systemName}; IP: {ipAddress}; PORT: {port}");
            Console.WriteLine("[Lighthouse] Performing pre-boot sanity check. Should be able to parse address [{0}]",
                selfAddress);
            Console.WriteLine("[Lighthouse] Parse successful.");


            var seeds = clusterConfig.GetStringList("akka.cluster.seed-nodes").ToList();

            Config injectedClusterConfigString = null;


            if (!seeds.Contains(selfAddress))
            {
                seeds.Add(selfAddress);

                if (seeds.Count > 1)
                {
                    injectedClusterConfigString = seeds.Aggregate("akka.cluster.seed-nodes = [",
                        (current, seed) => current + @"""" + seed + @""", ");
                    injectedClusterConfigString += "]";
                }
                else
                {
                    injectedClusterConfigString = "akka.cluster.seed-nodes = [\"" + selfAddress + "\"]";
                }
            }


            var finalConfig = injectedClusterConfigString != null
                ? injectedClusterConfigString
                    .WithFallback(clusterConfig)
                : clusterConfig;

            return ActorSystem.Create(systemName, finalConfig);
        }
    }
}