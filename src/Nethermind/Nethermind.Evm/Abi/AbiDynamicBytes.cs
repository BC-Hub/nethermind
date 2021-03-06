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
using System.Numerics;
using System.Text;
using Nethermind.Core.Extensions;

namespace Nethermind.Evm.Abi
{
    public class AbiDynamicBytes : AbiType
    {
        private const int PaddingMultiple = 32;

        public static AbiDynamicBytes Instance = new AbiDynamicBytes();

        private AbiDynamicBytes()
        {
        }

        public override bool IsDynamic => true;

        public override string Name => "bytes";

        public override Type CSharpType { get; } = typeof(byte[]);

        public override (object, int) Decode(byte[] data, int position)
        {
            (BigInteger length, int currentPosition) = UInt.DecodeUInt(data, position);
            int paddingSize = (1 + (int) length / PaddingMultiple) * PaddingMultiple;
            return (data.Slice(currentPosition, (int) length), currentPosition + paddingSize);
        }

        public override byte[] Encode(object arg)
        {
            if (arg is byte[] input)
            {
                int paddingSize = (1 + input.Length / PaddingMultiple) * PaddingMultiple;
                byte[] lengthEncoded = UInt.Encode(new BigInteger(input.Length));
                return Bytes.Concat(lengthEncoded, Bytes.PadRight(input, paddingSize));
            }

            if (arg is string stringInput)
            {
                return Encode(Encoding.ASCII.GetBytes(stringInput));
            }

            throw new AbiException(AbiEncodingExceptionMessage);
        }
    }
}