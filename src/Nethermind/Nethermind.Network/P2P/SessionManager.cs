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
using System.Collections.Specialized;
using System.Linq;
using Nethermind.Core;
using Nethermind.Core.Crypto;
using Nethermind.Network.P2P.Subprotocols.Eth;
using Nethermind.Network.P2P.Subprotocols.Eth.V63;
using Nethermind.Network.Rlpx;

namespace Nethermind.Network.P2P
{
    // TODO: this is close to ready to dynamically accept more protocols support but it seems unnecessary at the moment
    public class SessionManager : ISessionManager
    {
        private readonly int _listenPort;
        private readonly ILogger _logger;
        private readonly IMessageSerializationService _serializationService;
        private readonly PublicKey _localNodeId;

        private readonly Dictionary<string, ISession> _sessions = new Dictionary<string, ISession>();
        private Func<int, (string, int)> _adaptiveCodeResolver;

        public SessionManager(IMessageSerializationService serializationService, PublicKey localNodeId, int listenPort, ILogger logger)
        {
            _serializationService = serializationService;
            _localNodeId = localNodeId;
            _listenPort = listenPort;
            _logger = logger;
        }

        public (string, int) ResolveMessageCode(int adaptiveId)
        {
            return _adaptiveCodeResolver.Invoke(adaptiveId);
        }
        
        public void ReceiveMessage(Packet packet)
        {
            int dynamicMessageCode = packet.PacketType;
            (string protocol, int messageId) = ResolveMessageCode(dynamicMessageCode);
            _logger.Log($"Session Manager received a message (dynamic ID {dynamicMessageCode}. Resolved to {protocol}.{messageId}");

            if (protocol == null)
            {
                throw new InvalidProtocolException(packet.ProtocolType);
            }

            packet.PacketType = messageId;
            _sessions[protocol].HandleMessage(packet);
        }
        
        public void Start(string protocolCode, int version, IPacketSender packetSender, PublicKey remoteNodeId, int remotePort)
        {
            protocolCode = protocolCode.ToLowerInvariant();
            if (_sessions.ContainsKey(protocolCode))
            {
                throw new InvalidOperationException($"Session for protocol {protocolCode} already started");
            }

            if (protocolCode != Protocol.P2P && !_sessions.ContainsKey(Protocol.P2P))
            {
                throw new InvalidOperationException($"{Protocol.P2P} session has to be started before starting {protocolCode} session");
            }

            ISession session;
            switch (protocolCode)
            {
                case Protocol.P2P:
                    if (version != 5)
                    {
                        throw new NotSupportedException();
                    }
                    
                    session = new P2PSession(this, _serializationService, packetSender, _localNodeId, _listenPort, remoteNodeId, _logger);
                    break;
                case Protocol.Eth:
                    if (version < 62 || version > 63)
                    {
                        throw new NotSupportedException();
                    }
                    
                    session = version == 62
                        ? new Eth62Session(_serializationService, packetSender, _logger, remoteNodeId, remotePort)
                        : new Eth63Session(_serializationService, packetSender, _logger, remoteNodeId, remotePort);
                    break;
                default:
                    throw new NotSupportedException();
            }

            _sessions[protocolCode] = session;

            (string ProtocolCode, int SpaceSize)[] alphabetically = new (string, int)[_sessions.Count];
            alphabetically[0] = (Protocol.P2P, _sessions[Protocol.P2P].MessageIdSpaceSize);
            int i = 1;
            foreach (KeyValuePair<string, ISession> protocolSession in _sessions.Where(kv => kv.Key != "p2p").OrderBy(kv => kv.Key))
            {
                alphabetically[i++] = (protocolSession.Key, protocolSession.Value.MessageIdSpaceSize);
            }

            _adaptiveCodeResolver = dynamicId =>
            {
                int offset = 0;
                for (int j = 0; j < alphabetically.Length; j++)
                {
                    if (offset + alphabetically[j].SpaceSize > dynamicId)
                    {
                        return (alphabetically[j].ProtocolCode, dynamicId - offset);
                    }

                    offset += alphabetically[j].Item2;
                }

                return (null, 0);
            };

            session.Init();
        }

        public void Close(string protocolCode)
        {
            protocolCode = protocolCode.ToLowerInvariant();
            throw new NotImplementedException();
        }
    }
}