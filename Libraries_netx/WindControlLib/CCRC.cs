namespace WindControlLib
{

    /// <summary>
    /// CRC Check
    /// </summary>
    /// <remarks>
    /// http://www.codeproject.com/KB/cs/csRedundancyChckAlgorithm.aspx
    /// </remarks>
    public class CCRC8
    {

        /// <summary>
        /// Polynomial types
        /// </summary>
        public enum CRC8_POLY
        {
            CRC8 = 0xd5,
            CRC8_CCITT = 0x07,
            CRC8_DALLAS_MAXIM = 0x31,
            CRC8_SAE_J1850 = 0x1D,
            CRC_8_WCDMA = 0x9b,
            CRC_MinistimProgrammer = 0x01,
            CRC_8_PIC_16_18 = 0x00
        }

        private byte[] _Table = new byte[256];
        public byte[] Table
        {
            get { return _Table; }
            set { _Table = value; }
        }

        private readonly CRC8_POLY polynomial;

        public CCRC8(CRC8_POLY polynomial)
        {
            this.polynomial = polynomial;
            _Table = new byte[256];
            GenerateTable(polynomial);
        }


        /// <summary>
        /// Checks the CRC according to PIC24.
        /// </summary>
        /// <param name="data">data to check</param>
        /// <param name="CRC">The CRC.</param>
        /// <param name="LastByteIsCRC">if set to <c>true</c> [last byte in data is CRC, param CRC is ignored].</param>
        /// <param name="RemoveLastByte">if set to <c>true</c> [last byte in data is removed].</param>
        /// <returns>true if CRC check is OK</returns>
        public bool Check_CRC8(ref byte[] data, byte? CRC, bool LastByteIsCRC, bool RemoveLastByte)
        {
            byte CRC_soll = 0;  //CRC as it should be
            byte CRC_ist = 0;   //Calculated CRC

            try
            {
                if (LastByteIsCRC)
                {
                    CRC_soll = data[^1];
                    CRC_ist = Calc_CRC8(data, data.Length - 2);
                }
                else
                {
                    if (CRC != null)
                        CRC_soll = (byte)CRC;
                    else
                        throw new Exception("No valid CRC to compare with");

                    CRC_ist = Calc_CRC8(data, data.Length - 1);
                }

                if (RemoveLastByte)
                {

                    Array.Resize(ref data, data.Length - 1);
                }
            }
            catch
            { }


            return CRC_ist == CRC_soll;
        }


        /// <summary>
        /// Calcs the CRC according to PIC24.
        /// </summary>
        /// <param name="data">data</param>
        /// <param name="End_idx">CRC is clculated from data[0] ... data [End_idx]</param>
        /// <returns>CRC</returns>
        public byte Calc_CRC8(byte[] data, int End_idx)
        {
            if (data == null)
                throw new ArgumentNullException("val");

            byte c = 0;
            for (int i = 0; i <= End_idx; i++)
            {
                c = _Table[c ^ data[i]];
            }
            return c;
        }

        public byte Calc_CRC8(byte[] data)
        {
            return Calc_CRC8(data, data.Length - 1);
        }


        /// <summary>
        /// Calcs the CRC according to PIC24
        /// </summary>
        /// <param name="data">The data.</param>
        /// <param name="crc">previous crc</param>
        /// <returns></returns>
        private byte Calc_CRC8(byte data, byte crc)
        {
            return _Table[crc ^ data];
        }

        public void GenerateTable(CRC8_POLY polynomial)
        {
            if (polynomial == CRC8_POLY.CRC_8_PIC_16_18)
            {
                byte[] crc_array_Pic_16_18 = [
                    0x00, 0x5e, 0xbc, 0xe2, 0x61, 0x3f, 0xdd, 0x83,
                    0xc2, 0x9c, 0x7e, 0x20, 0xa3, 0xfd, 0x1f, 0x41,
                    0x9d, 0xc3, 0x21, 0x7f, 0xfc, 0xa2, 0x40, 0x1e,
                    0x5f, 0x01, 0xe3, 0xbd, 0x3e, 0x60, 0x82, 0xdc,
                    0x23, 0x7d, 0x9f, 0xc1, 0x42, 0x1c, 0xfe, 0xa0,
                    0xe1, 0xbf, 0x5d, 0x03, 0x80, 0xde, 0x3c, 0x62,
                    0xbe, 0xe0, 0x02, 0x5c, 0xdf, 0x81, 0x63, 0x3d,
                    0x7c, 0x22, 0xc0, 0x9e, 0x1d, 0x43, 0xa1, 0xff,
                    0x46, 0x18, 0xfa, 0xa4, 0x27, 0x79, 0x9b, 0xc5,
                    0x84, 0xda, 0x38, 0x66, 0xe5, 0xbb, 0x59, 0x07,
                    0xdb, 0x85, 0x67, 0x39, 0xba, 0xe4, 0x06, 0x58,
                    0x19, 0x47, 0xa5, 0xfb, 0x78, 0x26, 0xc4, 0x9a,
                    0x65, 0x3b, 0xd9, 0x87, 0x04, 0x5a, 0xb8, 0xe6,
                    0xa7, 0xf9, 0x1b, 0x45, 0xc6, 0x98, 0x7a, 0x24,
                    0xf8, 0xa6, 0x44, 0x1a, 0x99, 0xc7, 0x25, 0x7b,
                    0x3a, 0x64, 0x86, 0xd8, 0x5b, 0x05, 0xe7, 0xb9,
                    0x8c, 0xd2, 0x30, 0x6e, 0xed, 0xb3, 0x51, 0x0f,
                    0x4e, 0x10, 0xf2, 0xac, 0x2f, 0x71, 0x93, 0xcd,
                    0x11, 0x4f, 0xad, 0xf3, 0x70, 0x2e, 0xcc, 0x92,
                    0xd3, 0x8d, 0x6f, 0x31, 0xb2, 0xec, 0x0e, 0x50,
                    0xaf, 0xf1, 0x13, 0x4d, 0xce, 0x90, 0x72, 0x2c,
                    0x6d, 0x33, 0xd1, 0x8f, 0x0c, 0x52, 0xb0, 0xee,
                    0x32, 0x6c, 0x8e, 0xd0, 0x53, 0x0d, 0xef, 0xb1,
                    0xf0, 0xae, 0x4c, 0x12, 0x91, 0xcf, 0x2d, 0x73,
                    0xca, 0x94, 0x76, 0x28, 0xab, 0xf5, 0x17, 0x49,
                    0x08, 0x56, 0xb4, 0xea, 0x69, 0x37, 0xd5, 0x8b,
                    0x57, 0x09, 0xeb, 0xb5, 0x36, 0x68, 0x8a, 0xd4,
                    0x95, 0xcb, 0x29, 0x77, 0xf4, 0xaa, 0x48, 0x16,
                    0xe9, 0xb7, 0x55, 0x0b, 0x88, 0xd6, 0x34, 0x6a,
                    0x2b, 0x75, 0x97, 0xc9, 0x4a, 0x14, 0xf6, 0xa8,
                    0x74, 0x2a, 0xc8, 0x96, 0x15, 0x4b, 0xa9, 0xf7,
                    0xb6, 0xe8, 0x0a, 0x54, 0xd7, 0x89, 0x6b, 0x35
                ];
                Buffer.BlockCopy(crc_array_Pic_16_18, 0, _Table, 0, crc_array_Pic_16_18.Length);
            }
            else if (polynomial == CRC8_POLY.CRC_MinistimProgrammer)
            {
                byte[] crc_array_MinistimProgrammer = [
                    0x00, 0x07, 0x0E, 0x09, 0x1C, 0x1B, 0x12, 0x15,
                    0x38, 0x3F, 0x36, 0x31, 0x24, 0x23, 0x2A, 0x2D,
                    0x70, 0x77, 0x7E, 0x79, 0x6C, 0x6B, 0x62, 0x65,
                    0x48, 0x4F, 0x46, 0x41, 0x54, 0x53, 0x5A, 0x5D,
                    0xE0, 0xE7, 0xEE, 0xE9, 0xFC, 0xFB, 0xF2, 0xF5,
                    0xD8, 0xDF, 0xD6, 0xD1, 0xC4, 0xC3, 0xCA, 0xCD,
                    0x90, 0x97, 0x9E, 0x99, 0x8C, 0x8B, 0x82, 0x85,
                    0xA8, 0xAF, 0xA6, 0xA1, 0xB4, 0xB3, 0xBA, 0xBD,
                    0xC7, 0xC0, 0xC9, 0xCE, 0xDB, 0xDC, 0xD5, 0xD2,
                    0xFF, 0xF8, 0xF1, 0xF6, 0xE3, 0xE4, 0xED, 0xEA,
                    0xB7, 0xB0, 0xB9, 0xBE, 0xAB, 0xAC, 0xA5, 0xA2,
                    0x8F, 0x88, 0x81, 0x86, 0x93, 0x94, 0x9D, 0x9A,
                    0x27, 0x20, 0x29, 0x2E, 0x3B, 0x3C, 0x35, 0x32,
                    0x1F, 0x18, 0x11, 0x16, 0x03, 0x04, 0x0D, 0x0A,
                    0x57, 0x50, 0x59, 0x5E, 0x4B, 0x4C, 0x45, 0x42,
                    0x6F, 0x68, 0x61, 0x66, 0x73, 0x74, 0x7D, 0x7A,
                    0x89, 0x8E, 0x87, 0x80, 0x95, 0x92, 0x9B, 0x9C,
                    0xB1, 0xB6, 0xBF, 0xB8, 0xAD, 0xAA, 0xA3, 0xA4,
                    0xF9, 0xFE, 0xF7, 0xF0, 0xE5, 0xE2, 0xEB, 0xEC,
                    0xC1, 0xC6, 0xCF, 0xC8, 0xDD, 0xDA, 0xD3, 0xD4,
                    0x69, 0x6E, 0x67, 0x60, 0x75, 0x72, 0x7B, 0x7C,
                    0x51, 0x56, 0x5F, 0x58, 0x4D, 0x4A, 0x43, 0x44,
                    0x19, 0x1E, 0x17, 0x10, 0x05, 0x02, 0x0B, 0x0C,
                    0x21, 0x26, 0x2F, 0x28, 0x3D, 0x3A, 0x33, 0x34,
                    0x4E, 0x49, 0x40, 0x47, 0x52, 0x55, 0x5C, 0x5B,
                    0x76, 0x71, 0x78, 0x7F, 0x6A, 0x6D, 0x64, 0x63,
                    0x3E, 0x39, 0x30, 0x37, 0x22, 0x25, 0x2C, 0x2B,
                    0x06, 0x01, 0x08, 0x0F, 0x1A, 0x1D, 0x14, 0x13,
                    0xAE, 0xA9, 0xA0, 0xA7, 0xB2, 0xB5, 0xBC, 0xBB,
                    0x96, 0x91, 0x98, 0x9F, 0x8A, 0x8D, 0x84, 0x83,
                    0xDE, 0xD9, 0xD0, 0xD7, 0xC2, 0xC5, 0xCC, 0xCB,
                    0xE6, 0xE1, 0xE8, 0xEF, 0xFA, 0xFD, 0xF4, 0xF3
                ];
                Buffer.BlockCopy(crc_array_MinistimProgrammer, 0, _Table, 0, crc_array_MinistimProgrammer.Length);
            }
            else
            {
                for (int i = 0; i < _Table.Length; ++i)
                {
                    int curr = i;
                    for (int j = 0; j < 8; ++j)
                    {
                        if ((curr & 0x80) != 0)
                        {
                            curr = (curr << 1) ^ (int)this.polynomial;
                        }
                        else
                        {
                            curr <<= 1;
                        }
                    }
                    _Table[i] = (byte)curr;
                }
            }
        }

    }
}


