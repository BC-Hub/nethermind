﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Blockchain;
using Nethermind.Blockchain.Difficulty;
using Nethermind.Blockchain.Validators;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Core.Extensions;
using Nethermind.Core.Logging;
using Nethermind.Core.Specs;
using Nethermind.Dirichlet.Numerics;
using Nethermind.Evm;
using Nethermind.Mining;
using Nethermind.Store;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Ethereum.Test.Base
{
    public class BlockchainTestBase
    {
        private static ILogManager _logManager = NullLogManager.Instance;
        private static ILogger _logger = new SimpleConsoleLogger();
        
        private static readonly ISealEngine SealEngine = new EthashSealEngine(new Ethash(_logManager), _logManager); // temporarily keep reusing the same one as otherwise it would recreate cache for each test

        [SetUp]
        public void Setup()
        {
        }

        public static IEnumerable<BlockchainTest> LoadTests(string testSet)
        {
            Directory.SetCurrentDirectory(AppDomain.CurrentDomain.BaseDirectory);
            IEnumerable<string> testDirs = Directory.EnumerateDirectories(".", testSet);
            if (Directory.Exists(".\\Tests\\"))
            {
                testDirs = testDirs.Union(Directory.EnumerateDirectories(".\\Tests\\", testSet));
            }

            Dictionary<string, Dictionary<string, BlockchainTestJson>> testJsons = new Dictionary<string, Dictionary<string, BlockchainTestJson>>();
            foreach (string testDir in testDirs)
            {
                testJsons[testDir] = LoadTestsFromDirectory(testDir);
            }

            return testJsons.SelectMany(d => d.Value).Select(pair => Convert(pair.Key, pair.Value));
        }

        private static IReleaseSpec ParseSpec(string network)
        {
            switch (network)
            {
                case "Frontier":
                    return Frontier.Instance;
                case "Homestead":
                    return Homestead.Instance;
                case "TangerineWhistle":
                    return TangerineWhistle.Instance;
                case "SpuriousDragon":
                    return SpuriousDragon.Instance;
                case "EIP150":
                    return TangerineWhistle.Instance;
                case "EIP158":
                    return SpuriousDragon.Instance;
                case "Dao":
                    return Dao.Instance;
                case "Constantinople":
                    return Constantinople.Instance;
                case "Byzantium":
                    return Byzantium.Instance;
                default:
                    throw new NotSupportedException();
            }
        }

        private static Dictionary<string, BlockchainTestJson> LoadTestsFromDirectory(string testDir)
        {
            Dictionary<string, BlockchainTestJson> testsByName = new Dictionary<string, BlockchainTestJson>();
            List<string> testFiles = Directory.EnumerateFiles(testDir).ToList();
            foreach (string testFile in testFiles)
            {
                if (testFile.StartsWith(".stub"))
                {
                    continue;
                }
                
                string json = File.ReadAllText(testFile);
                Dictionary<string, BlockchainTestJson> testsInFile = JsonConvert.DeserializeObject<Dictionary<string, BlockchainTestJson>>(json);
                foreach (KeyValuePair<string, BlockchainTestJson> namedTest in testsInFile)
                {
                    string[] transitionInfo = namedTest.Value.Network.Split("At");
                    string[] networks = transitionInfo[0].Split("To");
                    for (int i = 0; i < networks.Length; i++)
                    {
                        networks[i] = networks[i].Replace("EIP150", "TangerineWhistle");
                        networks[i] = networks[i].Replace("EIP158", "SpuriousDragon");
                        networks[i] = networks[i].Replace("DAO", "Dao");
                    }

                    namedTest.Value.EthereumNetwork = ParseSpec(networks[0]);
                    if (transitionInfo.Length > 1)
                    {
                        namedTest.Value.TransitionBlockNumber = int.Parse(transitionInfo[1]);
                        namedTest.Value.EthereumNetworkAfterTransition = ParseSpec(networks[1]);
                    }

                    testsByName.Add(namedTest.Key, namedTest.Value);
                }
            }

            return testsByName;
        }

        private static AccountState Convert(AccountStateJson accountStateJson)
        {
            AccountState state = new AccountState();
            state.Balance = Bytes.FromHexString(accountStateJson.Balance).ToUInt256();
            state.Code = Bytes.FromHexString(accountStateJson.Code);
            state.Nonce = Bytes.FromHexString(accountStateJson.Nonce).ToUInt256();
            state.Storage = accountStateJson.Storage.ToDictionary(
                p => Bytes.FromHexString(p.Key).ToUInt256(),
                p => Bytes.FromHexString(p.Value));
            return state;
        }

        private class LoggingTraceListener : System.Diagnostics.TraceListener
        {
            private readonly StringBuilder _line = new StringBuilder();

            public override void Write(string message)
            {
                _line.Append(message);
            }

            public override void WriteLine(string message)
            {
                Write(message);
                _logger.Info(_line.ToString());
                _line.Clear();
            }
        }

        protected void Setup(ILogManager logManager)
        {
            _logManager = logManager ?? NullLogManager.Instance;
            _logger = _logManager.GetClassLogger();
        }

        protected async Task RunTest(BlockchainTest test, Stopwatch stopwatch = null)
        {
            LoggingTraceListener traceListener = new LoggingTraceListener();
            // TODO: not supported in .NET Core, need to replace?
//            Debug.Listeners.Clear();
//            Debug.Listeners.Add(traceListener);
            
            IDbProvider dbProvider = new MemDbProvider(_logManager);
            StateTree stateTree = new StateTree(dbProvider.GetOrCreateStateDb());
            

            ISpecProvider specProvider;
            if (test.NetworkAfterTransition != null)
            {
                specProvider = new CustomSpecProvider(
                    (0, Frontier.Instance),
                    (1, test.Network),
                    (test.TransitionBlockNumber, test.NetworkAfterTransition));
            }
            else
            {
                specProvider = new CustomSpecProvider(
                    (0, Frontier.Instance), // TODO: this thing took a lot of time to find after it was removed!, genesis block is always initialized with Frontier
                    (1, test.Network));
            }

            if (specProvider.GenesisSpec != Frontier.Instance)
            {
                Assert.Fail("Expected genesis spec to be Frontier for blockchain tests");
            }
            
            IDifficultyCalculator difficultyCalculator = new DifficultyCalculator(specProvider);
            IRewardCalculator rewardCalculator = new RewardCalculator(specProvider);
            
            IBlockTree blockTree = new BlockTree(new MemDb(), new MemDb(), specProvider, _logManager);
            IBlockhashProvider blockhashProvider = new BlockhashProvider(blockTree);
            ISignatureValidator signatureValidator = new SignatureValidator(ChainId.MainNet);
            ITransactionValidator transactionValidator = new TransactionValidator(signatureValidator);
            IHeaderValidator headerValidator = new HeaderValidator(difficultyCalculator, blockTree, SealEngine, specProvider, _logManager);
            IOmmersValidator ommersValidator = new OmmersValidator(blockTree, headerValidator, _logManager);
            IBlockValidator blockValidator = new BlockValidator(transactionValidator, headerValidator, ommersValidator, specProvider, _logManager);
            IStateProvider stateProvider = new StateProvider(stateTree, dbProvider.GetOrCreateCodeDb(), _logManager);
            IStorageProvider storageProvider = new StorageProvider(dbProvider, stateProvider, _logManager);
            IVirtualMachine virtualMachine = new VirtualMachine(
                stateProvider,
                storageProvider,
                blockhashProvider,
                _logManager);

            ISealEngine sealEngine = new EthashSealEngine(new Ethash(_logManager), _logManager);
            ITransactionStore transactionStore = new TransactionStore(new MemDb(), new MemDb(), specProvider);
            IEthereumSigner signer = new EthereumSigner(specProvider, _logManager);
            IBlockProcessor blockProcessor = new BlockProcessor(
                specProvider,
                blockValidator,
                rewardCalculator,
                new TransactionProcessor(
                    specProvider,
                    stateProvider,
                    storageProvider,
                    virtualMachine,
                    _logManager),
                dbProvider,
                stateProvider,
                storageProvider,
                transactionStore,
                _logManager);

            IBlockchainProcessor blockchainProcessor = new BlockchainProcessor(blockTree,
                sealEngine, 
                transactionStore, difficultyCalculator, blockProcessor, signer, _logManager, new PerfService(_logManager));

            InitializeTestState(test, stateProvider, storageProvider, specProvider);

            List<(Block Block, string ExpectedException)> correctRlpsBlocks = new List<(Block, string)>();
            for (int i = 0; i < test.Blocks.Length; i++)
            {
                try
                {
                    TestBlockJson testBlockJson = test.Blocks[i];
                    var rlpContext = Bytes.FromHexString(testBlockJson.Rlp).AsRlpContext();
                    Block suggestedBlock = Rlp.Decode<Block>(rlpContext);
                    suggestedBlock.Header.SealEngineType = test.SealEngineUsed ? SealEngineType.Ethash : SealEngineType.None;
                    
                    Assert.AreEqual(new Keccak(testBlockJson.BlockHeader.Hash), suggestedBlock.Header.Hash, "hash of the block");
                    for (int ommerIndex = 0; ommerIndex < suggestedBlock.Ommers.Length; ommerIndex++)
                    {
                        Assert.AreEqual(new Keccak(testBlockJson.UncleHeaders[ommerIndex].Hash), suggestedBlock.Ommers[ommerIndex].Hash, "hash of the ommer");    
                    }
                    
                    correctRlpsBlocks.Add((suggestedBlock, testBlockJson.ExpectedException));
                }
                catch (Exception e)
                {
                    _logger?.Info($"Invalid RLP ({i})");
                }
            }

            if (correctRlpsBlocks.Count == 0)
            {
                Assert.AreEqual(new Keccak(test.GenesisBlockHeader.Hash), test.LastBlockHash);
                return;
            }

            if (test.GenesisRlp == null)
            {
                test.GenesisRlp = Rlp.Encode(new Block(Convert(test.GenesisBlockHeader)));
            }

            Block genesisBlock = Rlp.Decode<Block>(test.GenesisRlp.Bytes);
            Assert.AreEqual(new Keccak(test.GenesisBlockHeader.Hash), genesisBlock.Header.Hash, "genesis header hash");
            
            ManualResetEvent genesisProcessed = new ManualResetEvent(false);
            blockTree.NewHeadBlock += (sender, args) =>
            {
                if (args.Block.Number == 0)
                {
                    Assert.AreEqual(genesisBlock.Header.StateRoot, stateTree.RootHash, "genesis state root");
                    genesisProcessed.Set();
                }
            };
                
            blockchainProcessor.Start();
            blockTree.SuggestBlock(genesisBlock);

            genesisProcessed.WaitOne();

            for (int i = 0; i < correctRlpsBlocks.Count; i++)
            {
                stopwatch?.Start();
                try
                {
                    if (correctRlpsBlocks[i].ExpectedException != null)
                    {
                        _logger.Info($"Expecting block exception: {correctRlpsBlocks[i].ExpectedException}");    
                    }

                    if (correctRlpsBlocks[i].Block.Hash == null)
                    {
                        throw new Exception($"null hash in {test.Name} block {i}");
                    }
                    
                    // TODO: mimic the actual behaviour where block goes through validating sync manager?
                    if (!test.SealEngineUsed || blockValidator.ValidateSuggestedBlock(correctRlpsBlocks[i].Block))
                    {
                        blockTree.SuggestBlock(correctRlpsBlocks[i].Block);
                    }
                    else
                    {
                        Console.WriteLine("Invalid block");
                    }
                }
                catch (InvalidBlockException ex)
                {
                }
                catch (Exception ex)
                {
                    _logger?.Info(ex.ToString());
                }
            }

            await blockchainProcessor.StopAsync(true);
            stopwatch?.Stop();

            RunAssertions(test, blockTree.RetrieveHeadBlock(), storageProvider, stateProvider);
        }

        private void InitializeTestState(BlockchainTest test, IStateProvider stateProvider, IStorageProvider storageProvider, ISpecProvider specProvider)
        {
            foreach (KeyValuePair<Address, AccountState> accountState in test.Pre)
            {
                foreach (KeyValuePair<UInt256, byte[]> storageItem in accountState.Value.Storage)
                {
                    storageProvider.Set(new StorageAddress(accountState.Key, storageItem.Key), storageItem.Value);
                }

                stateProvider.CreateAccount(accountState.Key, accountState.Value.Balance);
                Keccak codeHash = stateProvider.UpdateCode(accountState.Value.Code);
                stateProvider.UpdateCodeHash(accountState.Key, codeHash, specProvider.GenesisSpec);
                for (int i = 0; i < accountState.Value.Nonce; i++)
                {
                    stateProvider.IncrementNonce(accountState.Key);
                }
            }

            storageProvider.Commit(specProvider.GenesisSpec);
            stateProvider.Commit(specProvider.GenesisSpec);
        }

        private void RunAssertions(BlockchainTest test, Block headBlock, IStorageProvider storageProvider, IStateProvider stateProvider)
        {
            TestBlockHeaderJson testHeaderJson = test.Blocks
                                                     .Where(b => b.BlockHeader != null)
                                                     .SingleOrDefault(b => new Keccak(b.BlockHeader.Hash) == headBlock.Hash)?.BlockHeader ?? test.GenesisBlockHeader;
            BlockHeader testHeader = Convert(testHeaderJson);
            List<string> differences = new List<string>();
            foreach (KeyValuePair<Address, AccountState> accountState in test.PostState)
            {
                int differencesBefore = differences.Count;

                if (differences.Count > 8)
                {
                    Console.WriteLine("More than 8 differences...");
                    break;
                }

                bool accountExists = stateProvider.AccountExists(accountState.Key);
                BigInteger? balance = accountExists ? stateProvider.GetBalance(accountState.Key) : (BigInteger?)null;
                BigInteger? nonce = accountExists ? stateProvider.GetNonce(accountState.Key) : (BigInteger?)null;

                if (accountState.Value.Balance != balance)
                {
                    differences.Add($"{accountState.Key} balance exp: {accountState.Value.Balance}, actual: {balance}, diff: {balance - accountState.Value.Balance}");
                }

                if (accountState.Value.Nonce != nonce)
                {
                    differences.Add($"{accountState.Key} nonce exp: {accountState.Value.Nonce}, actual: {nonce}");
                }

                byte[] code = accountExists ? stateProvider.GetCode(accountState.Key) : new byte[0];
                if (!Bytes.AreEqual(accountState.Value.Code, code))
                {
                    differences.Add($"{accountState.Key} code exp: {accountState.Value.Code?.Length}, actual: {code?.Length}");
                }

                if (differences.Count != differencesBefore)
                {
                    _logger.Info($"ACCOUNT STATE ({accountState.Key}) HAS DIFFERENCES");    
                }

                differencesBefore = differences.Count;

                foreach (KeyValuePair<UInt256, byte[]> storageItem in accountState.Value.Storage)
                {
                    byte[] value = storageProvider.Get(new StorageAddress(accountState.Key, storageItem.Key)) ?? new byte[0];
                    if (!Bytes.AreEqual(storageItem.Value, value))
                    {
                        differences.Add($"{accountState.Key} storage[{storageItem.Key}] exp: {storageItem.Value.ToHexString(true)}, actual: {value.ToHexString(true)}");
                    }
                }

                if (differences.Count != differencesBefore)
                {
                    _logger.Info($"ACCOUNT STORAGE ({accountState.Key}) HAS DIFFERENCES");    
                }
            }


            BigInteger gasUsed = headBlock.Header.GasUsed;
            if ((testHeader?.GasUsed ?? 0) != gasUsed)
            {
                differences.Add($"GAS USED exp: {testHeader?.GasUsed ?? 0}, actual: {gasUsed}");
            }

            if (headBlock.Transactions.Any() && testHeader.Bloom.ToString() != headBlock.Header.Bloom.ToString())
            {
                differences.Add($"BLOOM exp: {testHeader.Bloom}, actual: {headBlock.Header.Bloom}");
            }

            if (testHeader.StateRoot != stateProvider.StateRoot)
            {
                differences.Add($"STATE ROOT exp: {testHeader.StateRoot}, actual: {stateProvider.StateRoot}");
            }

            if (testHeader.TransactionsRoot != headBlock.Header.TransactionsRoot)
            {
                differences.Add($"TRANSACTIONS ROOT exp: {testHeader.TransactionsRoot}, actual: {headBlock.Header.TransactionsRoot}");
            }

            if (testHeader.ReceiptsRoot != headBlock.Header.ReceiptsRoot)
            {
                differences.Add($"RECEIPT ROOT exp: {testHeader.ReceiptsRoot}, actual: {headBlock.Header.ReceiptsRoot}");
            }

            if (test.LastBlockHash != headBlock.Hash)
            {
                differences.Add($"LAST BLOCK HASH exp: {test.LastBlockHash}, actual: {headBlock.Hash}");
            }

            foreach (string difference in differences)
            {
                _logger.Info(difference);
            }

            Assert.Zero(differences.Count, "differences");
        }

        private static BlockHeader Convert(TestBlockHeaderJson headerJson)
        {
            if (headerJson == null)
            {
                return null;
            }

            BlockHeader header = new BlockHeader(
                new Keccak(headerJson.ParentHash),
                new Keccak(headerJson.UncleHash),
                new Address(headerJson.Coinbase),
                Bytes.FromHexString(headerJson.Difficulty).ToUInt256(),
                Bytes.FromHexString(headerJson.Number).ToUInt256(),
                (long)Bytes.FromHexString(headerJson.GasLimit).ToUnsignedBigInteger(),
                Bytes.FromHexString(headerJson.Timestamp).ToUInt256(),
                Bytes.FromHexString(headerJson.ExtraData)
            );
            
            header.Bloom = new Bloom(Bytes.FromHexString(headerJson.Bloom).ToBigEndianBitArray2048());
            header.GasUsed = (long)Bytes.FromHexString(headerJson.GasUsed).ToUnsignedBigInteger();
            header.Hash = new Keccak(headerJson.Hash);
            header.MixHash = new Keccak(headerJson.MixHash);
            header.Nonce = (ulong)Bytes.FromHexString(headerJson.Nonce).ToUnsignedBigInteger();
            header.ReceiptsRoot = new Keccak(headerJson.ReceiptTrie);
            header.StateRoot = new Keccak(headerJson.StateRoot);
            header.TransactionsRoot = new Keccak(headerJson.TransactionsTrie);
            return header;
        }

        private static Block Convert(TestBlockJson testBlockJson)
        {
            BlockHeader header = Convert(testBlockJson.BlockHeader);
            BlockHeader[] ommers = testBlockJson.UncleHeaders?.Select(Convert).ToArray() ?? new BlockHeader[0];
            Block block = new Block(header, ommers);
            block.Transactions = testBlockJson.Transactions?.Select(Convert).ToArray();
            return block;
        }

        private static Transaction Convert(TransactionJson transactionJson)
        {
            Transaction transaction = new Transaction();
            transaction.Value = Bytes.FromHexString(transactionJson.Value).ToUInt256();
            transaction.GasLimit = Bytes.FromHexString(transactionJson.GasLimit).ToUInt256();
            transaction.GasPrice = Bytes.FromHexString(transactionJson.GasPrice).ToUInt256();
            transaction.Nonce = Bytes.FromHexString(transactionJson.Nonce).ToUInt256();
            transaction.To = string.IsNullOrWhiteSpace(transactionJson.To) ? null : new Address(transactionJson.To);
            transaction.Data = transaction.To == null ? null : Bytes.FromHexString(transactionJson.Data);
            transaction.Init = transaction.To == null ? Bytes.FromHexString(transactionJson.Data) : null;
            Signature signature = new Signature(
                Bytes.FromHexString(transactionJson.R).PadLeft(32),
                Bytes.FromHexString(transactionJson.S).PadLeft(32),
                Bytes.FromHexString(transactionJson.V)[0]);
            transaction.Signature = signature;

            return transaction;
        }

        private static BlockchainTest Convert(string name, BlockchainTestJson testJson)
        {
            BlockchainTest test = new BlockchainTest();
            test.Name = name;
            test.Network = testJson.EthereumNetwork;
            test.NetworkAfterTransition = testJson.EthereumNetworkAfterTransition;
            test.TransitionBlockNumber = testJson.TransitionBlockNumber;
            test.LastBlockHash = new Keccak(testJson.LastBlockHash);
            test.GenesisRlp = testJson.GenesisRlp == null ? null : new Rlp(Bytes.FromHexString(testJson.GenesisRlp));
            test.GenesisBlockHeader = testJson.GenesisBlockHeader;
            test.Blocks = testJson.Blocks;
            test.PostState = testJson.PostState.ToDictionary(p => new Address(p.Key), p => Convert(p.Value));
            test.Pre = testJson.Pre.ToDictionary(p => new Address(p.Key), p => Convert(p.Value));
            return test;
        }
    }
}