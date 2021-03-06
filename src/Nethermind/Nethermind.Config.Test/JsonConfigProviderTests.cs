﻿using System;
using System.IO;
using System.Linq;
using Castle.Core.Logging;
using Nethermind.JsonRpc.Config;
using Nethermind.JsonRpc.DataModel;
using Nethermind.KeyStore.Config;
using Nethermind.Network.Config;
using Nethermind.Network.P2P;
using Nethermind.Stats;
using Nethermind.Stats.Model;
using NUnit.Framework;

namespace Nethermind.Config.Test
{
    [TestFixture]
    public class JsonConfigProviderTests
    {
        private JsonConfigProvider _configProvider;

        [SetUp]
        public void Initialize()
        {
            var keystoreConfig = new KeystoreConfig();
            var networkConfig = new NetworkConfig();
            var jsonRpcConfig = new JsonRpcConfig();
            var statsConfig = new StatsConfig();

            _configProvider = new JsonConfigProvider();
        }

        [Test]
        public void TestLoadJsonConfig()
        {
            _configProvider.LoadJsonConfig("SampleJsonConfig.json");

            var keystoreConfig = _configProvider.GetConfig<IKeystoreConfig>();
            var networkConfig = _configProvider.GetConfig<INetworkConfig>();
            var jsonRpcConfig = _configProvider.GetConfig<IJsonRpcConfig>();
            var statsConfig = _configProvider.GetConfig<IStatsConfig>();

            Assert.AreEqual(100, keystoreConfig.KdfparamsDklen);
            Assert.AreEqual("test", keystoreConfig.Cipher);

            Assert.AreEqual("test", jsonRpcConfig.JsonRpcVersion);           
            Assert.AreEqual("UTF7", jsonRpcConfig.MessageEncoding);
            Assert.AreEqual(2, jsonRpcConfig.EnabledModules.Count());
            new[] { ModuleType.Eth, ModuleType.Shh }.ToList().ForEach(x =>
            {
                Assert.IsTrue(jsonRpcConfig.EnabledModules.Contains(x));
            });

            Assert.AreEqual(4, networkConfig.Concurrency);
            Assert.AreEqual(3, statsConfig.PenalizedReputationLocalDisconnectReasons.Length);
            new[] { DisconnectReason.UnexpectedIdentity, DisconnectReason.IncompatibleP2PVersion, DisconnectReason.BreachOfProtocol }
                .ToList().ForEach(x =>
            {
                Assert.IsTrue(statsConfig.PenalizedReputationLocalDisconnectReasons.Contains(x));
            });
            Assert.AreEqual(2, networkConfig.BootNodes.Length);

            var node1 = networkConfig.BootNodes.FirstOrDefault(x => x.NodeId == "testNodeId");
            Assert.IsNotNull(node1);
            Assert.AreEqual("testHist", node1.Host);
            Assert.AreEqual(43, node1.Port);

            var node2 = networkConfig.BootNodes.FirstOrDefault(x => x.NodeId == "testNodeId2");
            Assert.IsNotNull(node2);
            Assert.AreEqual("testHist2", node2.Host);
            Assert.AreEqual(44, node2.Port);
        }
    }
}
