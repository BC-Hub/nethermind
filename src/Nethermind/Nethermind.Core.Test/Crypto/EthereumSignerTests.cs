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

using Nethermind.Core.Crypto;
using Nethermind.Core.Encoding;
using Nethermind.Core.Extensions;
using Nethermind.Core.Logging;
using Nethermind.Core.Specs;
using Nethermind.Core.Test.Builders;
using NUnit.Framework;

namespace Nethermind.Core.Test.Crypto
{
    [TestFixture]
    public class EthereumSignerTests
    {   
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(1000000)]
        [TestCase(1700000)]
        [TestCase(2000000)]
        public void Signature_test_ropsten(int blockNumber)
        {
            EthereumSigner signer = new EthereumSigner(RopstenSpecProvider.Instance, NullLogManager.Instance);
            PrivateKey key = Build.A.PrivateKey.TestObject;
            Transaction tx = Build.A.Transaction.TestObject;
            signer.Sign(key, tx, blockNumber);
            Address address = signer.RecoverAddress(tx, blockNumber);
            Assert.AreEqual(key.Address, address);
        }
        
        [Test]
        public void Test_eip155_for_the_first_ropsten_transaction()
        {
            Transaction tx = Rlp.Decode<Transaction>(new Rlp(Bytes.FromHexString("0xf85f808082520894353535353535353535353535353535353535353580801ca08d24b906be2d91a0bf2168862726991cc408cddf94cb087b392ce992573be891a077964b4e55a5c8ec7b85087d619c641c06def33ab052331337ca9efcd6b82aef")));
            
            Assert.AreEqual(new Keccak("0x5fd225549ed5c587c843e04578bdd4240fc0d7ab61f8e9faa37e84ec8dc8766d"), tx.Hash, "hash");
            EthereumSigner signer = new EthereumSigner(RopstenSpecProvider.Instance, NullLogManager.Instance);
            Address from = signer.RecoverAddress(tx, 11);
            Assert.AreEqual(new Address("0x874b54a8bd152966d63f706bae1ffeb0411921e5"), from, "from");
        }
        
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(1000000)]
        [TestCase(1700000)]
        [TestCase(2000000)]
        public void Signature_test_olympic(int blockNumber)
        {
            EthereumSigner signer = new EthereumSigner(OlympicSpecProvider.Instance, NullLogManager.Instance);
            PrivateKey key = Build.A.PrivateKey.TestObject;
            Transaction tx = Build.A.Transaction.TestObject;
            signer.Sign(key, tx, blockNumber);
            Address address = signer.RecoverAddress(tx, blockNumber);
            Assert.AreEqual(key.Address, address);
        }
    }
}