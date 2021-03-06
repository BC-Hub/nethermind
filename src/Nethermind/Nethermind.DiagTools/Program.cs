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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Nethermind.Core;
using Nethermind.Evm;
using Newtonsoft.Json;

namespace Nethermind.DiagTools
{
    internal class Program
    {
        private static TxTraceCompare _comparer = new TxTraceCompare();
        private static HttpClient _client = new HttpClient();
        private static IJsonSerializer _serializer = new UnforgivingJsonSerializer();

        public static async Task Main(params string[] args)
        {
            var transactionHashes = Directory.GetFiles(@"D:\tx_traces\nethermind").Select(Path.GetFileNameWithoutExtension).ToArray();
            for (int i = 0; i < transactionHashes.Length; i++)
            {
                string txHash = transactionHashes[i];
                Console.WriteLine($"comparing {txHash} ({i} of {transactionHashes.Length})");
                try
                {
                    TransactionTrace gethTrace = await DownloadGethTrace(txHash);
                    TransactionTrace nethTrace = await DownloadNethTrace(txHash);
                    _comparer.Compare(gethTrace, nethTrace);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"  failed at {i} with {e}");
                }
            }

            Console.WriteLine("Complete");
            Console.ReadLine();
        }

        private static async Task<TransactionTrace> DownloadNethTrace(string txHash)
        {
            string nethPath = "D:\\tx_traces\\neth_" + txHash + ".txt";
            string text;

            WrappedTransactionTrace nethTrace;
            if (!File.Exists(nethPath))
            {
                HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:8345");
                msg.Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"method\":\"debug_traceTransaction\",\"params\":[\"{txHash}\"],\"id\":42}}");
                msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage rsp = await _client.SendAsync(msg);
                text = await rsp.Content.ReadAsStringAsync();
                nethTrace = _serializer.Deserialize<WrappedTransactionTrace>(text);
                text = _serializer.Serialize(nethTrace, true);
                File.WriteAllText(nethPath, text);
            }
            else
            {
                text = File.ReadAllText(nethPath);
                nethTrace = _serializer.Deserialize<WrappedTransactionTrace>(text);                
            }
            
            return nethTrace.Result;
        }

        private static async Task<TransactionTrace> DownloadGethTrace(string txHash)
        {
            string gethPath = "D:\\tx_traces\\geth_" + txHash + ".txt";
            string text;

            WrappedTransactionTrace gethTrace;
            if (!File.Exists(gethPath))
            {
                HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Post, "http://94.237.53.197:8545");
                msg.Content = new StringContent($"{{\"jsonrpc\":\"2.0\",\"method\":\"debug_traceTransaction\",\"params\":[\"{txHash}\"],\"id\":42}}");
                msg.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                HttpResponseMessage rsp = await _client.SendAsync(msg);
                text = await rsp.Content.ReadAsStringAsync();
                gethTrace = _serializer.Deserialize<WrappedTransactionTrace>(text);
                text = _serializer.Serialize(gethTrace, true);
                File.WriteAllText(gethPath, text);
            }
            else
            {
                text = File.ReadAllText(gethPath);
                gethTrace = _serializer.Deserialize<WrappedTransactionTrace>(text);                
            }
            
            return gethTrace.Result;
        }
    }
}