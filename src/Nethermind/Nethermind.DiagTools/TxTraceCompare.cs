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

using Nethermind.Core.Extensions;
using Nethermind.Core.Logging;
using Nethermind.Evm;

namespace Nethermind.DiagTools
{
    public class TxTraceCompare
    {
        private ILogger _logger = new SimpleConsoleLogger();

        public void Compare(TransactionTrace gethTrace, TransactionTrace nethTrace)
        {
            if (gethTrace.Gas != nethTrace.Gas)
            {
                _logger.Warn($"  gas geth {gethTrace.Gas} != neth {nethTrace.Gas} (diff: {gethTrace.Gas - nethTrace.Gas})");
            }

//            string gethReturnValue = gethTrace.ReturnValue?.ToHexString();
//            string nethReturnValue = nethTrace.ReturnValue?.ToHexString();
//            if (gethReturnValue != nethReturnValue)
//            {
//                _logger.Warn($"  return value geth {gethReturnValue} != neth {nethReturnValue}");
//            }
            
            if (gethTrace.Failed != nethTrace.Failed)
            {
                _logger.Warn($"  failed diff geth {gethTrace.Failed} != neth {nethTrace.Failed}");
            }

            int ixDiff = 0;
            for (int i = 0; i < gethTrace.Entries.Count; i++)
            {
//                _logger.Info($"  comparing evm entry {i}");
                var gethEntry = gethTrace.Entries[i];
                if (gethEntry.Error != null)
                {
                    ixDiff++;
                    continue;
                }

                int nethIx = i - ixDiff;
                
                string entryDesc = $"ix {i}/{nethIx} pc {gethTrace.Entries[i].Pc} op {gethTrace.Entries[i].Operation} gas {gethTrace.Entries[i].Gas} | ";
                if (nethTrace.Entries.Count < nethIx + 1)
                {
                    _logger.Warn($"    neth entry missing");        
                }
                
                var nethEntry = nethTrace.Entries[nethIx];
                if (gethEntry.Operation != nethEntry.Operation)
                {
                    _logger.Warn($"    {entryDesc} operation geth {gethEntry.Operation} neth {nethEntry.Operation}");
                    break;
                }
                
                if (gethEntry.Gas != nethEntry.Gas)
                {
                    _logger.Warn($"    {entryDesc} gas geth {gethEntry.Gas} neth {nethEntry.Gas}");
                    break;
                }
                
                if (gethEntry.GasCost != nethEntry.GasCost)
                {
                    _logger.Warn($"    {entryDesc} gas cost geth {gethEntry.GasCost} neth {nethEntry.GasCost}");
                    break;
                }
            }
        }
    }
}