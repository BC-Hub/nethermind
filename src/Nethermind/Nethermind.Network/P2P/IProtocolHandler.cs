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
using Nethermind.Network.Rlpx;
using Nethermind.Stats;
using Nethermind.Stats.Model;

namespace Nethermind.Network.P2P
{
    public interface IProtocolHandler
    {
        byte ProtocolVersion { get; }
        string ProtocolCode { get; }
        int MessageIdSpaceSize { get; }
        
        void Init();
        void HandleMessage(Packet message);
        //TODO is close needed if we have disconnect?
        void Close();
        void Disconnect(DisconnectReason disconnectReason);

        event EventHandler<ProtocolInitializedEventArgs> ProtocolInitialized;
        event EventHandler<ProtocolEventArgs> SubprotocolRequested;
    }
}