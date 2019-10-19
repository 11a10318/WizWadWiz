using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

namespace WizWadWiz
{
    partial class Program
    {
        public static byte[] Zipper(FileList[] entries)
        {
            //Convert current time to DOS time (gross)
            //Dos time is the current time+date, compressed into just 4 bytes.
            //Due to the extreme compression, the time is only accurate to two seconds.
            //It also can't calculate dates before 1980, or after 2107, so expect it to break if you're a time-traveller (If you're using this tool after 2107, there's something wrong with *you*, not the tool)
            uint Time = 0;
            Time |= (uint)(DateTime.Now.Second / 2) << 0;
            Time |= (uint)DateTime.Now.Minute << 5;
            Time |= (uint)DateTime.Now.Hour << 11;
            Time |= (uint)DateTime.Now.Day << 16;
            Time |= (uint)DateTime.Now.Month << 21;
            Time |= (uint)(DateTime.Now.Year - 1980) << 25;
            byte[] timebytes = BitConverter.GetBytes(Time);
            Console.WriteLine("Dostime: {0}", Time.ToString("X2"));
            //Array.Reverse(timebytes);   //Have to reverse the bytes because the zip is in LE

            //Environment.Exit(0);
            //byte[] output = new byte[0];
            //Things to keep track of:
            //Offsets of each entry
            //Total entries (entries.Length)
            //Total length of written data (for offset of central-directory)
            List<byte> EntireZip = new List<byte>();    //Byte list to store the entire zip in memory (list for dynamic-ness)
            List<byte> ZipFooter = new List<byte>();    //Byte list to store the zip's footer
            for (int i = 0; i < entries.Length; i++)
            {
                int offset = EntireZip.Count;  //Save the current entry offset (for use in the footer)
                EntireZip.AddRange(new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x14, 0x00, 0x00, 0x00 });  //Add PK header (v20 minver, no flags)
                if (entries[i].IsCompressed)
                    EntireZip.AddRange(new byte[] { 0x08, 0x00 });  //Mark file as a deflate stream (that's how they're compressed)
                else    //If the file isn't compressed
                    EntireZip.AddRange(new byte[] { 0x00, 0x00 });  //Mark the file as non-compressed
                EntireZip.AddRange(timebytes);  //Add the time bytes (modified time of when the script started, because I cbf'd reading the actual time from the file attributes, plus processing time would increase)
                EntireZip.AddRange(BitConverter.GetBytes(entries[i].CRC));
                EntireZip.AddRange(BitConverter.GetBytes(entries[i].CompressedSize - 6));
                EntireZip.AddRange(BitConverter.GetBytes(entries[i].Size));
                EntireZip.AddRange(BitConverter.GetBytes((short)entries[i].Filename.Length));
                EntireZip.AddRange(new byte[] { 0x00, 0x00 });  //Add two null bytes for extra data length (we don't have any extra data)
                EntireZip.AddRange(ASCIIEncoding.ASCII.GetBytes(entries[i].Filename));
                EntireZip.AddRange(entries[i].Data);    //Copy the file to the zip stream


                ZipFooter.AddRange(new byte[] { 0x50, 0x4B, 0x01, 0x02, 0x14, 0x00, 0x14, 0x00, 0x00, 0x00 });  //Add footer_header
                if (entries[i].IsCompressed)
                    ZipFooter.AddRange(new byte[] { 0x08, 0x00 });  //Mark file as a deflate stream (that's how they're compressed)
                else    //If the file isn't compressed
                    ZipFooter.AddRange(new byte[] { 0x00, 0x00 });  //Mark the file as non-compressed
                ZipFooter.AddRange(timebytes);
                ZipFooter.AddRange(BitConverter.GetBytes(entries[i].CRC));
                ZipFooter.AddRange(BitConverter.GetBytes(entries[i].CompressedSize - 6));
                ZipFooter.AddRange(BitConverter.GetBytes(entries[i].Size));
                ZipFooter.AddRange(BitConverter.GetBytes((short)entries[i].Filename.Length));
                ZipFooter.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x20, 0x00, 0x00, 0x00 });  //Extra field length, comment length, disk #, internal attribs (00 00 for binary), external attribs (02 00 00 00 seems ok?)
                ZipFooter.AddRange(BitConverter.GetBytes(offset));  //Add the offset for the file
                ZipFooter.AddRange(ASCIIEncoding.ASCII.GetBytes(entries[i].Filename));  //Add the filename
            }

            int FooterOffset = EntireZip.Count;   //Remember where the first byte of the footer is located in the zip

            EntireZip.AddRange(ZipFooter);  //Add the footer to the zip data
            //Now we need to add the zip64 footer, because some wads have a *lot* of files (eg; root.wad has over 73,000 files), and the max file-count for zip is 65535 (ffff)

            //Zip64 End of central directory
            EntireZip.AddRange(new byte[] { 0x50, 0x4B, 0x06, 0x06, 0x2C, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x2D, 0x00, 0x2D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });  //PKZIOP 64 footer_header_marker, size of this field (excluding size and magic), version of zip (creator and viewer), disk number (0), disk number that contains directory (0)
            EntireZip.AddRange(BitConverter.GetBytes((long)entries.Length)); //Number of entries (including number of directories D:)
            EntireZip.AddRange(BitConverter.GetBytes((long)entries.Length));
            EntireZip.AddRange(BitConverter.GetBytes((long)ZipFooter.Count));
            EntireZip.AddRange(BitConverter.GetBytes((long)FooterOffset));

            //ZIP64 End of central directory locator
            EntireZip.AddRange(new byte[] { 0x50, 0x4B, 0x06, 0x07, 0x00, 0x00, 0x00, 0x00 });  //magic, disk with this stuff on it
            EntireZip.AddRange(BitConverter.GetBytes((long)(FooterOffset + ZipFooter.Count)));  //Offset of this is after the footer?
            EntireZip.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });  //Number of disks (0)

            //End of central directory
            EntireZip.AddRange(new byte[] { 0x50, 0x4B, 0x05, 0x06, 0x00, 0x00, 0x00, 0x00 });  //Footer_footer, number of disks, disk the footer is located on (all 0 because we ain't splittin nothin')
            if (entries.Length >= 65535)    //If the max files is beyond the max file-count storable in the zip footer (65535 is ffff), set the filecount to ffff (even windows does this, so the filecount is probable just a legacy field)
                EntireZip.AddRange(new byte[] { 0xff, 0xff, 0xff, 0xff });
            else
            {
                EntireZip.AddRange(BitConverter.GetBytes((ushort)entries.Length));  //Add number of files in this disk of archive (we only have one 'disk', so just do the total filecount)
                EntireZip.AddRange(BitConverter.GetBytes((ushort)entries.Length));  //Add number of files in entire archive
            }
            EntireZip.AddRange(BitConverter.GetBytes(ZipFooter.Count));
            EntireZip.AddRange(BitConverter.GetBytes(FooterOffset));
            EntireZip.AddRange(new byte[] { 0x00, 0x00 });

            return EntireZip.ToArray();
        }
    }
}
