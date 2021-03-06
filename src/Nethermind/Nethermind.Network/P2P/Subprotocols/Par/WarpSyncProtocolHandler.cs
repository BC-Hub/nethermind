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
using Nethermind.Core.Crypto;
using Nethermind.Network.Rlpx;
using Nethermind.Stats;
using Nethermind.Stats.Model;

namespace Nethermind.Network.P2P.Subprotocols.Par
{
    public class WarpSyncProtocolHandler : IProtocolHandler
    {
        public byte ProtocolVersion { get; } = 2;
        public string ProtocolCode { get; } = "par";
        public void Init()
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            throw new NotImplementedException();
        }

        public void Disconnect(DisconnectReason disconnectReason)
        {
        }

        public int MessageIdSpaceSize { get; } = 10;
        public void HandleMessage(Packet message)
        {
            throw new NotImplementedException();
        }

        public event EventHandler<ProtocolInitializedEventArgs> ProtocolInitialized;
        public event EventHandler<ProtocolEventArgs> SubprotocolRequested;
    }
}