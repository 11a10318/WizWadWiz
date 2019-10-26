using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace WizWadWiz
{
    partial class Program
    {
        /// <summary>
        /// Reads .wad entries and stores them in a FileList array (including compressed data)
        /// </summary>
        /// <param name="wad"></param>
        /// <returns></returns>
        public static FileList[] ReadWad(string wad)
        {
            BinaryReader reader = new BinaryReader(new FileStream(wad,FileMode.Open));  //Add a BinaryReader handle to the memorystream (allows for easier reading)

            string header = new string(reader.ReadChars(5)); //Read the first 5 bytes, to see if the file is a KIWAD
            if (header != "KIWAD")   //If the header is not 'KIWAD'
            {
                Console.WriteLine("Invalid wad!\nThis tool is only intended for use with KingsIsle .wad files!");
                Quit();
            }

            int version = reader.ReadInt32();   //Read .wad version
            int FileCount = reader.ReadInt32(); //Read filecount

            FileList[] entries = new FileList[FileCount];   //Array that will contain every file entry in the wad

            if (version >= 2)   //If the wad is version 2 or later
                reader.ReadByte();  //Read a byte that is only found in wad revision 2+

            StringBuilder dummies = new StringBuilder(entries.Length);

            for (int i = 0; i < FileCount; i++)  //For every file entry in the wad, grab its offset, sizes, compression-status, crc, and name, and add that to an array
            {
                entries[i].Offset = reader.ReadUInt32();    //Read file offset
                entries[i].Size = reader.ReadUInt32(); ;  //Read size
                entries[i].CompressedSize = reader.ReadUInt32(); //Read compressed size
                entries[i].IsCompressed = reader.ReadBoolean(); //Read compression byte (whether the file is compressed or not)
                entries[i].CRC = reader.ReadUInt32();   //Read crc
                int namelen = reader.ReadInt32();   //Read length of name
                entries[i].Filename = new string(reader.ReadChars(namelen)).Replace("\0", String.Empty); //Read name (using specified name length), replace trailing null byte with empty
                long tempoffset = reader.BaseStream.Position;
                reader.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin);
                if (reader.ReadUInt32() != 0)
                {
                    reader.BaseStream.Seek(-4, SeekOrigin.Current);
                    if (entries[i].IsCompressed)
                        entries[i].Data = reader.ReadBytes((int)entries[i].CompressedSize);
                    else
                        entries[i].Data = reader.ReadBytes((int)entries[i].Size);
                }
                else
                {
                    dummies.AppendLine(entries[i].Filename + " Is a dummy file!");
                    entries[i].Data = new byte[]{0x00,0x00,0x00,0x00};
                    entries[i].IsCompressed = false;
                    entries[i].Size = 4;
                    entries[i].CompressedSize = 4;
                }
                reader.BaseStream.Seek(tempoffset, SeekOrigin.Begin);
            }
            Console.WriteLine(dummies);
            return entries; //Return the FileList array for the input wad

        }

    }
}
