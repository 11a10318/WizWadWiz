/*
Simplified BSD License

Copyright 2017 Derek Will

Redistribution and use in source and binary forms, with or without modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.

2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer 
in the documentation and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, 
INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. 
IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, 
OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, 
OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, 
OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY 
OF SUCH DAMAGE.
*/

using System;
using System.Linq;
using Ionic.Crc;

namespace CrcSharp
{

    /// <summary>
    /// CRC algorithm.
    /// </summary>
    public class Crc
    {
        private readonly CrcParameters _parameters;
        private readonly ulong[] _lookupTable;

        /// <summary>
        /// Gets the CRC algorithm parameters.
        /// </summary>
        /// <value>The CRC algorithm parameters.</value>
        public CrcParameters Parameters
        {
            get
            {
                return _parameters;
            }
        }

        /// <summary>
        /// Gets the lookup table used in calculating check values.
        /// </summary>
        /// <value>The lookup table.</value>
        public ulong[] LookupTable
        {
            get
            {
                return _lookupTable;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CrcSharp.Crc"/> class.
        /// </summary>
        /// <param name="parameters">CRC algorithm parameters.</param>
        public Crc(CrcParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters", "Parameters argument cannot be null.");

            _parameters = parameters;
            _lookupTable = GenerateLookupTable();
        }

        /// <summary>
        /// Calculates the CRC check value as a numeric value.
        /// </summary>
        /// <returns>The CRC check value as a numeric value.</returns>
        /// <param name="data">Data to compute the check value of.</param>
        public ulong CalculateAsNumeric(byte[] data)
        {
            byte[] crcCheckVal = CalculateCheckValue(data);
            Array.Resize(ref crcCheckVal, 8);
            return BitConverter.ToUInt64(crcCheckVal, 0);
        }

        /// <summary>
        /// Calculates the CRC check value as a byte array.
        /// </summary>
        /// <returns>The CRC check value as a byte array.</returns>
        /// <param name="data">Data to compute the check value of.</param>
        public byte[] CalculateCheckValue(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data", "Data argument cannot be null.");

            ulong crc = _parameters.InitialValue;

            if (_parameters.ReflectIn)
            {
                crc = ReflectBits(crc, _parameters.Width);
            }

            foreach (byte b in data)
            {
                if (_parameters.ReflectIn)
                {
                    crc = _lookupTable[(crc ^ b) & 0xFF] ^ (crc >> 8);
                }
                else
                {
                    crc = _lookupTable[((crc >> (_parameters.Width - 8)) ^ b) & 0xFF] ^ (crc << 8);
                }

                crc &= (UInt64.MaxValue >> (64 - _parameters.Width));
            }

            // Source: https://stackoverflow.com/questions/28656471/how-to-configure-calculation-of-crc-table/28661073#28661073
            // Per Mark Adler - ...the reflect out different from the reflect in (CRC-12/3GPP). 
            // In that one case, you need to bit reverse the output since the input is not reflected, but the output is.
            if (_parameters.ReflectIn ^ _parameters.ReflectOut)
            {
                crc = ReflectBits(crc, _parameters.Width);
            }

            ulong crcFinalValue = crc ^ _parameters.XorOutValue;
            return BitConverter.GetBytes(crcFinalValue).Take((_parameters.Width + 7) / 8).ToArray();
        }

        /// <summary>
        /// Generates the lookup table using the CRC algorithm parameters.
        /// </summary>
        /// <returns>The lookup table.</returns>
        private ulong[] GenerateLookupTable()
        {
            if (_parameters == null)
                throw new InvalidOperationException("CRC parameters must be set prior to calling this method.");

            var lookupTable = new ulong[256];
            ulong topBit = (ulong)((ulong)1 << (_parameters.Width - 1));

            for (int i = 0; i < lookupTable.Length; i++)
            {
                byte inByte = (byte)i;
                if (_parameters.ReflectIn)
                {
                    inByte = (byte)ReflectBits(inByte, 8);
                }

                ulong r = (ulong)((ulong)inByte << (_parameters.Width - 8));
                for (int j = 0; j < 8; j++)
                {
                    if ((r & topBit) != 0)
                    {
                        r = ((r << 1) ^ _parameters.Polynomial);
                    }
                    else
                    {
                        r = (r << 1);
                    }
                }

                if (_parameters.ReflectIn)
                {
                    r = ReflectBits(r, _parameters.Width);
                }

                lookupTable[i] = r & (UInt64.MaxValue >> (64 - _parameters.Width));
            }

            return lookupTable;
        }

        /// <summary>
        /// Reflects the bits of a provided numeric value.
        /// </summary>
        /// <returns>Bit-reflected version of the provided numeric value.</returns>
        /// <param name="b">Value to reflect the bits of.</param>
        /// <param name="bitCount">Number of bits in the provided value.</param>
        private static ulong ReflectBits(ulong b, int bitCount)
        {
            ulong reflection = 0x00;

            for (int bitNumber = 0; bitNumber < bitCount; ++bitNumber)
            {
                if (((b >> bitNumber) & 0x01) == 0x01)
                {
                    reflection |= (ulong)(((ulong)1 << ((bitCount - 1) - bitNumber)));
                }
            }

            return reflection;
        }
    }

    /// <summary>
    /// CRC algorithm parameters.
    /// </summary>
    public class CrcParameters
    {
        private readonly int _width;
        private readonly ulong _polynomial;
        private readonly ulong _initialValue;
        private readonly ulong _xorOutValue;
        private readonly bool _reflectIn;
        private readonly bool _reflectOut;

        /// <summary>
        /// Gets the width of the CRC algorithm in bits.
        /// </summary>
        /// <value>The width of the CRC algorithm in bits.</value>
        public int Width
        {
            get
            {
                return _width;
            }
        }

        /// <summary>
        /// Gets the polynomial of the CRC algorithm.
        /// </summary>
        /// <value>The polynomial of the CRC algorithm.</value>
        public ulong Polynomial
        {
            get
            {
                return _polynomial;
            }
        }

        /// <summary>
        /// Gets the initial value used in the computation of the CRC check value.
        /// </summary>
        /// <value>The initial value used in the computation of the CRC check value.</value>
        public ulong InitialValue
        {
            get
            {
                return _initialValue;
            }
        }

        /// <summary>
        /// Gets the value which is XORed to the final computed value before returning the check value.
        /// </summary>
        /// <value>The value which is XORed to the final computed value before returning the check value.</value>
        public ulong XorOutValue
        {
            get
            {
                return _xorOutValue;
            }
        }

        /// <summary>
        /// Gets a value indicating whether bytes are reflected before being processed.
        /// </summary>
        /// <value><c>true</c> if each byte is to be reflected before being processed; otherwise, <c>false</c>.</value>
        public bool ReflectIn
        {
            get
            {
                return _reflectIn;
            }
        }

        /// <summary>
        /// Gets a value indicating whether the final computed value is reflected before the XOR stage.
        /// </summary>
        /// <value><c>true</c> if the final computed value is reflected before the XOR stage; otherwise, <c>false</c>.</value>
        public bool ReflectOut
        {
            get
            {
                return _reflectOut;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CrcSharp.CrcParameters"/> class.
        /// </summary>
        /// <param name="width">Width of the CRC algorithm in bits.</param>
        /// <param name="polynomial">Polynomial of the CRC algorithm.</param>
        /// <param name="initialValue">Initial value used in the computation of the CRC check value.</param>
        /// <param name="xorOutValue">The value which is XORed to the final computed value before returning the check value.</param>
        /// <param name="reflectIn">If set to <c>true</c> each byte is to be reflected before being processed.</param>
        /// <param name="reflectOut">If set to <c>true</c> the final computed value is reflected before the XOR stage.</param>
        public CrcParameters(int width, ulong polynomial, ulong initialValue, ulong xorOutValue, bool reflectIn, bool reflectOut)
        {
            ThrowIfParametersInvalid(width, polynomial, initialValue, xorOutValue);

            _width = width;
            _polynomial = polynomial;
            _initialValue = initialValue;
            _xorOutValue = xorOutValue;
            _reflectIn = reflectIn;
            _reflectOut = reflectOut;
        }

        /// <summary>
        /// Verifies if the parameter values are valid.
        /// </summary>
        /// <param name="width">Width of the CRC algorithm in bits.</param>
        /// <param name="polynomial">Polynomial of the CRC algorithm.</param>
        /// <param name="initialValue">Initial value used in the computation of the CRC check value.</param>
        /// <param name="xorOutValue">The value which is XORed to the final computed value before returning the check value.</param>
        private void ThrowIfParametersInvalid(int width, ulong polynomial, ulong initialValue, ulong xorOutValue)
        {
            if (width < 8 || width > 64)
                throw new ArgumentOutOfRangeException("width", "Width must be between 8-64 bits.");

            ulong maxValue = (UInt64.MaxValue >> (64 - width));

            if (polynomial > maxValue)
                throw new ArgumentOutOfRangeException("polynomial", string.Format("Polynomial exceeds {0} bits.", width));

            if (initialValue > maxValue)
                throw new ArgumentOutOfRangeException("initialValue", string.Format("Initial Value exceeds {0} bits.", width));

            if (xorOutValue > maxValue)
                throw new ArgumentOutOfRangeException("xorOutValue", string.Format("XOR Out Value exceeds {0} bits.", width));
        }
    }
}