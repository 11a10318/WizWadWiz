//Wizard101 Wad Wizard V1.0
//You know you love my program names :P
//
//
//Scans supplied wad, grabs all filenames and offsets
//Saves filename,size, compressed size, and offset
//Provides following arguments:
//
//!DONE!//-i (info: dumps all info)
//!DONE!//-x (extract) [filename (* for all files)] [Directory name to extract into]
//-a (add: Add a file to the wad) [file to insert] [directory\name inside wad]
//-r (remove: Removes a file from the wad) [directory\name of file to remove]
//-c (create: Creates a wad, based on files in the specified directory) [directory of files to put in wad]
//!DONE!//-d (diff: Compares two wads, and lists different files) [wad to compare] {Optional: directory to extract different files to}
//Would be useful for dataminers when the game updates

//A GUI extension can use -i to get the file info
//With this, it can allow the user to explore the files and extract individual files as wanted
//This is awesome, because it allows you to extract single files from a wad, without having to extract the entire wad first
//
//Potential issues:
//
//  Escape characters:
//      Could be fixed by disallowing symbols that most filesystems don't allow (eg; :, or '\0 : \0' (null colon null))
//      I like null colon null, because it's not only guaranteed to be missing from every file, it's also fairly simple-looking (user just sees ':')
//          Although, using three characters isn't the most efficient method
//          Maybe just colon will suffice, as there aren't any filenames with a colon, and ntfs+hfs+fat32 all disallow the semicolon in filenames, so it's fairly unlikely to cause problems
//          It might open up a vulnerability in the way it reads filenames (eg; if a filename in a wad contains a colon, it will break, but I'm not too worried)
//
//  Malicious .wad files:
//      What happens if a file says it will extract to 100 bytes, but it's really 200?
//          I should ignore expected filesize when extracting, and if the file exceeds the buffer, show a warning.
//              Give an option to ignore the error and proceed, or just cancel (increase buffer size)
//          C# is pretty safe, so I shouldn't really have to worry about this (safeguards ftw)
//
//  CRC-Collision:
//      diff-checking can fail if the files differ, but the CRC doesn't
//      This also happens if the crc data in the wad is spoofed
//      If KI randomly decided to set all CRC data to 00000000, then diff-checking would fail
//      The workaround for this would be to calculate the checksum myself, rather than relying on the included CRC
//          This takes more time, and would still be vulnerable to crc-collisions
//          To remedy this, I could use a stronger hash function, which could hurt performance even more (although, most suitable hash functions should be fast enough on a modern machine)
//
//I don't really know how to verify the checksums included in .wad files
//It's not too important, because my program is pretty safe with extraction, so there shouldn't be any data corruption.
//I'd like to get checksums figured out at some point though, just for that extra bit of safety
//
//In the meantime, I can use the checksum for diff-checking!
//That's pretty cool, because we'd be able to diff-check without extracting anything.
//If it's all done in memory, it'll be very fast.
//The other alternative would be to calculate the CRC of the compressed data, which would be slightly slower, but still much faster than diff-checking files on disk
//
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Core;

namespace WizWadWiz
{
    partial class Program
    {

        public struct FileList
        {
            public string Filename;
            public uint Offset;
            public uint Size;
            public uint CompressedSize;
            public bool IsCompressed;
            public uint CRC;
            //public byte[] Decompressed;
            public byte[] Data;
        }

        static void Main(string[] args)
        {
            //Zipper(new FileList[1]);
            //args = new string[2];
            //args[0] = "Root.wad";
            //args[1] = "-1";
            System.Diagnostics.Stopwatch MainTimer = new System.Diagnostics.Stopwatch();
            MainTimer.Start();
            string wad = "";    //wad filename
            string mode = "";   //Operating mode
            string arg1 = "";   //Argument 1 for mode
            string arg2 = "";   //Argument 2 for mode

            //Try grabbing wad name and mode
            try
            {
                wad = args[0];
                mode = args[1];
            }
            catch   //If the wad/mode couldn't be grabbed
            {
                Console.WriteLine("Please specify wad and mode!\n");    //Print error message
                PrintHelp();    //Print usage info
            }
            
            //Try grabbing arguments for specified mode
            try
            {
                if (mode == "-x" || mode == "-a")   //If extract/add mode, grab two arguments
                {
                    arg1 = args[2];
                    arg2 = args[3];
                }
                else if (mode == "-r" || mode == "-c" || mode == "-d")  //Remove/Create/Diff mode, one arg
                    arg1 = args[2];
                else if (mode != "-i" && mode != "-1")    //If the mode is not -i (takes no arguments), then we don't know what mode they specified
                {
                    Console.WriteLine("Invalid mode!"); //Print error
                    PrintHelp();    //Print usage info
                }
            }
            catch   //If the arguments were missing, or something else went wrong
            {
                Console.WriteLine("Invalid arguments for specified mode!"); //Print an error
                PrintHelp();    //Print usage info
            }

            //Init empty variables, so they can be modified in the if condition below
            MemoryStream instream = new MemoryStream();
            //FileStream outstream;
            BinaryReader reader = new BinaryReader(instream);
            //BinaryWriter writer = new BinaryWriter(null);

            byte[] inwad = new byte[0];

            if (mode != "-c")    //If the tool is not in create mode, check if the wad exists
            {
                if (! System.IO.File.Exists(wad))
                {
                    Console.WriteLine("Wad file not found!");
                    PrintHelp();
                }
                else    //If the file exists
                {
                    inwad = File.ReadAllBytes(wad);
                    instream = new MemoryStream(inwad);  //Read the file into a memorystream
                    reader = new BinaryReader(instream);  //Add a BinaryReader handle to the memorystream (allows for easier reading)
                }
            }

            if(mode == "-d" && !File.Exists(arg1))  //If using diff mode, and the second file doesn't exist
            {
                Console.WriteLine("Second wad does not exist!");
                PrintHelp();
            }

            
            if (mode == "-x")
            {
                if (!Directory.Exists(arg2))    //If the output directory doesn't exist
                    Directory.CreateDirectory(arg2);    //Create the output directory
            }

            /*else
            {
                if (!System.IO.File.Exists(wad))
                {
                    outstream = new FileStream(wad, System.IO.FileMode.Create);
                    writer = new BinaryWriter(outstream);
                }
                else
                {
                    Console.WriteLine("Wad already exists! Please specify a different name, or delete the existing file");
                    PrintHelp();
                }
            }*/

            if(mode == "-1")
            {
                Console.WriteLine("dev mode 1 (in-memory extraction test)");
                string header = new string(reader.ReadChars(5));    //Skip the header
                if(header != "KIWAD")
                {
                    Console.WriteLine("What the fuck are you doing? That's not a wad D:");
                    Environment.Exit(0);
                }
                int version = reader.ReadInt32();   //.wad version
                int FileCount = reader.ReadInt32(); //number of files

                FileList[] entries = new FileList[FileCount];
                //byte[] OneGB = new byte[1000000000];
                //MemoryStream OneGBMS = new MemoryStream(OneGB);

                if (version >= 2)
                    reader.ReadByte();

                for (int i = 0; i < FileCount; i++)  //For every file entry in the wad, grab its offset, sizes, compression-status, crc, and name, and add that to an array
                {
                    entries[i].Offset = reader.ReadUInt32();    //Read file offset
                    entries[i].Size = reader.ReadUInt32(); ;  //Read size
                    entries[i].CompressedSize = reader.ReadUInt32(); //Read compressed size
                    entries[i].IsCompressed = reader.ReadBoolean(); //Read compression byte (whether the file is compressed or not)
                    entries[i].CRC = reader.ReadUInt32();   //Read crc
                    int namelen = reader.ReadInt32();   //Read length of name
                    entries[i].Filename = new string(reader.ReadChars(namelen)).Replace("\0", String.Empty); //Read name (using specified name length), replace trailing null byte with empty
                }

                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();
                StringBuilder CRCLIST = new StringBuilder();
                object locker = new object();

                Parallel.For(0, entries.Length, i =>
                {
                    MemoryStream instream_local = new MemoryStream(inwad);
                    BinaryReader reader_local = new BinaryReader(instream_local);
                    reader_local.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek to the file entry

                    if (reader_local.ReadInt32() != 0)    //Read 4 bytes of the file. If the bytes aren't 0 (the file exists)
                    {
                        reader_local.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek the stream back four bytes (because we just read 4 bytes of data, which would have been skipped if we didn't seek backwards

                        byte[] filemem = new byte[0];


                        if (entries[i].IsCompressed)   //If the file is marked as compressed
                        {
                            filemem = reader_local.ReadBytes((int)entries[i].CompressedSize);    //Create a memorystream for the file (size is the compressed filesize)
                            entries[i].Data = new byte[filemem.Length-6];  //Copy the compressed data to the entry's data section
                            Array.Copy(filemem, 2, entries[i].Data, 0, filemem.Length - 6);
                            filemem = Ionic.Zlib.ZlibStream.UncompressBuffer(filemem);
                        }
                        else    //If the file isn't compressed
                        {
                            filemem = reader_local.ReadBytes((int)entries[i].Size);    //Create a memorystream for the file (size is the uncompressed filesize)
                            entries[i].Data = filemem;  //Copy the data to the entry's data section
                        }
                        Ionic.Crc.CRC32 crc = new Ionic.Crc.CRC32();
                        entries[i].CRC = (uint)crc.GetCrc32(new MemoryStream(filemem)); //Replace the entries' crc with the CRC of the compressed data (KI are shit and use their own incompatible polynomials for their checksum, so we need to recalculate it with a standard polynomial)
                    }
                    else    //If the first four bytes are 0 (dummy data)
                        Console.WriteLine("Missing File: " + entries[i].Filename);  //Inform the user, and move on to the next entry

                    instream_local.Dispose();
                    reader_local.Dispose();

                });
                stopwatch.Stop();

                System.Diagnostics.Stopwatch ziptimer = new System.Diagnostics.Stopwatch();
                ziptimer.Start();

                byte[] outputfile = Zipper(entries);

                /*
                var outputMemStream = new MemoryStream();
                using (var zipStream = new ZipOutputStream(outputMemStream))
                {
                    zipStream.SetLevel(0);

                    for (int i = 0; i < entries.Length; i++)
                    {
                        ZipEntry newEntry = new ZipEntry(entries[i].Filename);
                        newEntry.DateTime = DateTime.Now;
                        zipStream.PutNextEntry(newEntry);
                        zipStream.Write(entries[i].Decompressed, 0, entries[i].Decompressed.Length);
                        //StreamUtils.Copy(new MemoryStream(entries[i].Decompressed), zipStream, new byte[4096]);
                        zipStream.CloseEntry();
                    }
                    zipStream.IsStreamOwner = false;
                }*/


                ziptimer.Stop();
                //outputMemStream.Position = 0;
                //outputMemStream.CopyTo(OneGBMS);

                File.WriteAllBytes("MEGAWAD.ZIP", outputfile);
                //Console.WriteLine(CRCLIST);

                //File.WriteAllBytes("MEGAWAD", OneGB);
                MainTimer.Stop();

                Console.WriteLine("Decompressed {0} files in {1} Milliseconds", FileCount, stopwatch.ElapsedMilliseconds);
                //Console.WriteLine("Zipped in {0} Ms", ziptimer.ElapsedMilliseconds);
                Console.WriteLine("Total program runtime: {0}", MainTimer.ElapsedMilliseconds);
                Environment.Exit(0);



            }


            if (mode == "-i" || mode == "-x" || mode == "-d")
            {
                string header = new string(reader.ReadChars(5)); //Read the first 5 bytes, to see if the file is a KIWAD
                if(header != "KIWAD")   //If the header is not 'KIWAD'
                {
                    Console.WriteLine("File specified is not a KIWAD file!");
                    Environment.Exit(0);
                }
                
                int version = reader.ReadInt32();   //Read .wad version
                int FileCount = reader.ReadInt32(); //Read filecount

                FileList[] entries = new FileList[FileCount];   //Array that will contain every file entry in the wad


                if (version >= 2)   //If the wad is version 2 or later
                    reader.ReadByte();  //Read a byte that is only found in wad revision 2+

                for(int i = 0; i < FileCount; i++)  //For every file entry in the wad, grab its offset, sizes, compression-status, crc, and name, and add that to an array
                {
                    entries[i].Offset = reader.ReadUInt32();    //Read file offset
                    entries[i].Size = reader.ReadUInt32(); ;  //Read size
                    entries[i].CompressedSize = reader.ReadUInt32(); //Read compressed size
                    entries[i].IsCompressed = reader.ReadBoolean(); //Read compression byte (whether the file is compressed or not)
                    entries[i].CRC = reader.ReadUInt32();   //Read crc
                    int namelen = reader.ReadInt32();   //Read length of name
                    entries[i].Filename = new string(reader.ReadChars(namelen)).Replace("\0",String.Empty); //Read name (using specified name length), replace trailing null byte with empty
                }

                if (mode == "-i")   //If using info mode
                {
                    StringBuilder sb = new StringBuilder(); //Make a new stringbuilder (stringbuilder is much faster than just appending strings normally)
                    for (int i = 0; i < FileCount; i++) //For each file entry
                    {
                        sb.AppendLine(entries[i].Offset.ToString("X") + ":" + entries[i].CompressedSize + ":" + entries[i].Size + ":" + entries[i].Filename);   //Add a newline to the output string, with the offset and filename (offset:filename)
                    }
                    Console.WriteLine(sb);  //Print the output string
                    MainTimer.Stop();
                    Console.WriteLine("{0} files found in {1} milliseconds",FileCount,MainTimer.ElapsedMilliseconds);
                    Environment.Exit(0);    //Quit
                }
                else if (mode == "-d")  //If using diff mode
                {
                    Console.WriteLine("Diff-checking {0} and {1}", wad, arg1);
                    bool extract = false;

                    try     //Try grabbing the output-folder argument
                    {
                        arg2 = args[3]; //Grab fourth argument (starting from 0)
                        try
                        {
                            Console.WriteLine("Output folder specified: {0}", arg2);    //Just some verbosity
                            if (!Directory.Exists(arg2))    //If the specified output directory doesn't exist
                                Directory.CreateDirectory(arg2);    //Create the specified output directory
                            Console.WriteLine("Created Directory: {0}", arg2);  //Inform the user of the directory creation
                            extract = true; //Enable file-extraction
                        }
                        catch   //If something went wrong whlie creating the directory
                        {
                            Console.WriteLine("Error creating directory: {0}\nMaybe you're trying to write to a folder you don't have permission to write in?", arg2);  //Let the user know something went wrong
                            Environment.Exit(0);    //Exit
                        }
                    }
                    catch   //If there isn't an output-folder argument
                    {
                        Console.WriteLine("No output folder specified. No extraction will be performed.");  //Print a message stating operating mode
                    }

                    Console.WriteLine("Checking differences...");
                    inwad = File.ReadAllBytes(arg1);
                    instream = new MemoryStream(inwad);  //Read the file into a memorystream
                    reader = new BinaryReader(instream);  //Add a BinaryReader handle to the memorystream (allows for easier reading)

                    header = new string(reader.ReadChars(5)); //Read the first 5 bytes, to see if the file is a KIWAD
                    if (header != "KIWAD")   //If the header is not 'KIWAD'
                    {
                        Console.WriteLine("Second File is not a KIWAD file!");
                        Environment.Exit(0);
                    }

                    version = reader.ReadInt32();   //Read .wad version
                    int FileCount2 = reader.ReadInt32(); //Read filecount

                    FileList[] entries2 = new FileList[FileCount2];
                    bool[] ExtractIt = new bool[FileCount2];    //Create a bool array, which keeps track of which files to extract from the second wad

                    if (version >= 2)   //If the wad is version 2 or later
                        reader.ReadByte();  //Read a byte that is only found in wad revision 2+

                    for (int i = 0; i < FileCount2; i++)  //For every file entry in the second wad
                    {
                        entries2[i].Offset = reader.ReadUInt32();    //Read file offset
                        entries2[i].Size = reader.ReadUInt32(); ;  //Read size
                        entries2[i].CompressedSize = reader.ReadUInt32(); //Read compressed size
                        entries2[i].IsCompressed = reader.ReadBoolean(); //Read compression byte (whether the file is compressed or not)
                        entries2[i].CRC = reader.ReadUInt32();   //Read crc
                        int namelen = reader.ReadInt32();   //Read length of name
                        entries2[i].Filename = new string(reader.ReadChars(namelen)).Replace("\0", String.Empty); //Read name (using specified name length), replace trailing null byte with empty
                    }

                    StringBuilder MissingIn2 = new StringBuilder();
                    StringBuilder DiffIn2 = new StringBuilder();

                    object sync = new object();
                    Parallel.For(0, entries.Length, i =>    //For each file entry in the first wad
                    { 
                        bool Exists = false;    //Mark the file as non-existant in both wads 
                        for (int j = 0; j < FileCount2; j++)  //For each file in second wad
                        {
                            if (entries2[j].Filename == entries[i].Filename)  //If the currently-processes filename in wad 1 is also the same filename in wad2
                            {
                                Exists = true;  //Mark the file as existing in both
                                if (entries2[j].CRC != entries[i].CRC)  //If the files have a different CRC
                                {
                                    lock (sync) //Use a lock to prevent SB from getting corrupt by multiple-accesses
                                    {
                                        DiffIn2.AppendLine(entries[i].Filename);    //Add the filename to the 'DiffIn2' string, so we can print the results later
                                        ExtractIt[j] = true;    //Mark the file in wad2 for extraction, because it's different
                                    }
                                }
                                break;  //Stop searching for this file
                            }
                        }
                        if (!Exists)    //If the file wasn't marked as existing
                        {
                            lock (sync) //Use a lock to prevent SB from getting corrupt by multiple-accesses
                            {
                                MissingIn2.AppendLine(entries[i].Filename); //Add the file to the 'MissingIn2' string for printing later
                            }
                        }
                    });

                    StringBuilder MissingIn1 = new StringBuilder();

                    Parallel.For(0, FileCount2, i =>                        //For each file entry in the second wad
                    {
                        bool Exists = false;
                        for (int j = 0; j < FileCount; j++)
                        {
                            if (entries2[i].Filename == entries[j].Filename)
                            {
                                Exists = true;  //Markn the file as existing in both
                                break;  //Stop searching for this file
                            }
                        }
                        if (!Exists)    //If the file wasn't marked as existing
                        {
                            lock(sync)  //Use a lock to prevent SB from getting corrupt by multiple-accesses
                            {
                                MissingIn1.AppendLine(entries[i].Filename); //Add the file to the 'MissingIn1' string for printing later
                                ExtractIt[i] = true;    //Mark the file in wad2 for extraction, because it doesn't exist in wad1
                            }
                        }
                    });

                    Console.WriteLine("Diff-checking complete!");
                    if (extract)    //If the user specified an output directory (extract diff files)
                    {
                        Console.WriteLine("Extracting new/different files..."); //Inform the user that the files will now be extracted

                        Console.WriteLine("Pre-creating directories...");
                        for (int i = 0; i < FileCount2; i++)    //For each file in the second wad
                        {
                            if (ExtractIt[i])   //If the file is marked for extraction, check it for any subdirectories, and create them if necessary
                                PreCreate(entries[i], arg2);    //Call 'PreCreate' for that file, and any required subdirs for that file will be created
                                
                            /*
                             {
                                if (entries2[i].Filename.Contains('\\') || entries2[i].Filename.Contains('/'))  //If the filename contains a directory
                                {

                                    int slashindex = entries2[i].Filename.LastIndexOf('\\'); //Grab the last \ in the filename (grab the last subdirectory directory)
                                    if (slashindex < 0)
                                        slashindex = entries2[i].Filename.LastIndexOf('/');

                                    if (!Directory.Exists(entries2[i].Filename.Substring(0, slashindex)))  //If the directory\subdirectory doesn't exist
                                    {
                                        Directory.CreateDirectory(arg2 + "\\" + entries2[i].Filename.Substring(0, slashindex));    //Create the directory\subdirectory
                                    }
                                }
                            }
                            */
                        }
                        Console.WriteLine("Directories created!\nExtracting...");
                        Parallel.For(0, FileCount2, i =>    //For each file in the second wad
                          {
                          if (ExtractIt[i])
                          {
                              MemoryStream instream_local = new MemoryStream(inwad);
                              BinaryReader reader_local = new BinaryReader(instream_local);
                              reader_local.BaseStream.Seek(entries2[i].Offset, SeekOrigin.Begin); //Seek to the file entry

                              if (reader_local.ReadInt32() != 0)    //Read 4 bytes of the file. If the bytes aren't 0 (the file exists)
                              {
                                  reader_local.BaseStream.Seek(entries2[i].Offset, SeekOrigin.Begin); //Seek the stream back four bytes (because we just read 4 bytes of data, which would have been skipped if we didn't seek backwards

                                  byte[] filemem = new byte[0];


                                      if (entries2[i].IsCompressed)   //If the file is marked as compressed
                                      {
                                          filemem = reader_local.ReadBytes((int)entries2[i].CompressedSize);    //Create a memorystream for the file (size is the compressed filesize)
                                          filemem = Ionic.Zlib.ZlibStream.UncompressBuffer(filemem);
                                      }
                                      else    //If the file isn't compressed
                                      {
                                          filemem = reader_local.ReadBytes((int)entries2[i].Size);    //Create a memorystream for the file (size is the uncompressed filesize)
                                      }

                                      using (FileStream output = new FileStream(arg2 + "\\" + entries2[i].Filename, FileMode.Create))    //Create the file that is being extracted (replaces old files if they exist)
                                      {
                                          output.Write(filemem, 0, filemem.Length);   //Write the file from memory to disk
                                      }



                                  }
                                  else    //If the first four bytes are 0 (dummy data)
                                      Console.WriteLine("Missing File: " + entries2[i].Filename);  //Inform the user, and move on to the next entry

                                  instream_local.Dispose();
                                  reader_local.Dispose();
                              }
                          });

                    }



                    if (MissingIn1.Length > 0 || MissingIn2.Length > 0 || DiffIn2.Length > 0)   //If there were any file differences
                    {
                        Console.WriteLine("Differences found!");
                        if (FileCount != FileCount2)
                            Console.WriteLine("========================================File count is different!========================================\nOld:{0}\tNew:{1}", FileCount, FileCount2);
                        if (MissingIn1.Length > 0)
                            Console.WriteLine("========================================Files missing in first wad========================================\n{0}", MissingIn1);
                        if (MissingIn2.Length > 0)
                            Console.WriteLine("========================================Files missing in second wad========================================\n{0}", MissingIn2);
                        if (DiffIn2.Length > 0)
                            Console.WriteLine("========================================Files changed========================================\n{0}", DiffIn2);
                    }
                    else
                        Console.WriteLine("No differences found!");

                    MainTimer.Stop();
                    Console.WriteLine("Total program runtime: {0}Ms", MainTimer.ElapsedMilliseconds);
                    Environment.Exit(0);
                }
                else if (mode == "-x")   //If using extract mode
                {
                    if(arg1 != "*")   //If the user specified a file to extract (not all files)
                    {
                        for(int i = 0; i < FileCount; i++)  //For each file in the filelist
                        {
                            if (string.Equals(entries[i].Filename,arg1,StringComparison.OrdinalIgnoreCase) || string.Equals(entries[i].Filename.Replace('/', '\\'), arg1, StringComparison.OrdinalIgnoreCase))   //If the file entry matches the user-specified file (ignoring case and slash-direction)
                            {
                                Console.WriteLine("File found!");

                                //Check if file is located in a subdirectory. If so, create the appropriate directory structure
                                PreCreate(entries[i], arg2);

                                reader.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek to the file entry

                                if (reader.ReadInt32() != 0)    //Read 4 bytes of the file. If the bytes aren't 0 (the file exists)
                                {
                                    reader.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek the stream back four bytes (because we just read 4 bytes of data, which would have been skipped if we didn't seek backwards

                                    byte[] filemem = new byte[0];


                                    if (entries[i].IsCompressed)   //If the file is marked as compressed
                                    {
                                        filemem = reader.ReadBytes((int)entries[i].CompressedSize);    //Create a memorystream for the file (size is the compressed filesize)
                                        filemem = Ionic.Zlib.ZlibStream.UncompressBuffer(filemem);
                                    }
                                    else    //If the file isn't compressed
                                    {
                                        filemem = reader.ReadBytes((int)entries[i].Size);    //Create a memorystream for the file (size is the uncompressed filesize)
                                    }
                                    
                                    using (FileStream output = new FileStream(arg2 + "\\" + entries[i].Filename, FileMode.Create))    //Create the file that is being extracted (replaces old files if they exist)
                                    {
                                        output.Write(filemem, 0, filemem.Length);   //Write the file from memory to disk
                                        Console.WriteLine("File extracted to: {0}", arg2 + "\\" + entries[i].Filename.Replace('/','\\'));
                                    }
                                    
                                }
                                else    //If the first four bytes are 0 (dummy data)
                                    Console.WriteLine("Empty File: " + entries[i].Filename);  //Inform the user, and move on to the next entry

                                return;
                            }
                            //If the filename doesn't match, read the next entry
                        }
                        //If all files have been scanned, and the user-specified file wasn't found; let the user know, and give them some advice
                        Console.WriteLine("'{0}' was not found in the specified wad!",arg1);
                        Console.WriteLine("Make sure you include the file's parent directories");
                        Console.WriteLine("eg: capabilities\\cpu.xml");
                        return;
                    }
                    else    //If the file is '*": Don't 'really' need to say else here, because it would have exited previously anyway, but it just makes things more readable
                    {
                        
                        //Create directories in advance, using a single thread (prevents race crash when parallel threads attempt to create the same directory at the same time)
                        Console.WriteLine("Pre-creating directories...");
                        for (int i = 0; i < FileCount; i++)
                            PreCreate(entries[i], arg2);    //Pre-create any subdirectories listed in the filename (arg2 is the base directory for extraction)
                        Console.WriteLine("Directories created!\nExtracting...");

                        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                        stopwatch.Start();

                        Parallel.For(0,entries.Length, i =>
                        {
                            //Console.WriteLine("[{0}]:{1}",stopwatch.ElapsedTicks, entry.Filename);

                            MemoryStream instream_local = new MemoryStream(inwad);
                            BinaryReader reader_local = new BinaryReader(instream_local);
                            reader_local.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek to the file entry

                            if (reader_local.ReadInt32() != 0)    //Read 4 bytes of the file. If the bytes aren't 0 (the file exists)
                            {
                                reader_local.BaseStream.Seek(entries[i].Offset, SeekOrigin.Begin); //Seek the stream back four bytes (because we just read 4 bytes of data, which would have been skipped if we didn't seek backwards

                                byte[] filemem = new byte[0];


                                if (entries[i].IsCompressed)   //If the file is marked as compressed
                                {
                                    filemem = reader_local.ReadBytes((int)entries[i].CompressedSize);    //Create a memorystream for the file (size is the compressed filesize)
                                    filemem = Ionic.Zlib.ZlibStream.UncompressBuffer(filemem);
                                }
                                else    //If the file isn't compressed
                                {
                                    filemem = reader_local.ReadBytes((int)entries[i].Size);    //Create a memorystream for the file (size is the uncompressed filesize)
                                }

                                entries[i].Data = filemem;  //Save the file (extracted if needed), to the entry's data field
                            }
                            else    //If the first four bytes are 0 (dummy data)
                                Console.WriteLine("Missing File: " + entries[i].Filename);  //Inform the user, and move on to the next entry

                            instream_local.Dispose();
                            reader_local.Dispose();

                        }
                        );

                        stopwatch.Stop();
                        Console.WriteLine("Extraction complete!\nWriting to disk... (this may take some time)");
                        System.Diagnostics.Stopwatch writetimer = new System.Diagnostics.Stopwatch();
                        writetimer.Start();

                        Parallel.For(0, entries.Length, i =>
                         {
                             File.WriteAllBytes(arg2 + "\\" + entries[i].Filename, entries[i].Data);
                         }); 

                        writetimer.Stop();
                        MainTimer.Stop();
                        Console.WriteLine("Extracted {0} files in {1} Milliseconds", FileCount, stopwatch.ElapsedMilliseconds);
                        Console.WriteLine("Wrote files in {0}Ms", writetimer.ElapsedMilliseconds);
                        Console.WriteLine("Total program runtime: {0}", MainTimer.ElapsedMilliseconds);
                        return; //Exit

                    }

                }

            }


            //Console.ReadLine();
        }

        static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("www.exe [wad] [mode] [arguments]\n");
            Console.WriteLine("Modes:");
            Console.WriteLine("-i (info: prints info about contained info)");
            Console.WriteLine("-x (extract) [filename (* for all files)] [directory to extract into]");
            Console.WriteLine("-a (add: Add a file to the wad) [file to insert] [directory\\name inside wad]");
            Console.WriteLine("-r (remove: Removes a file from the wad) [directory\\name of file to remove]");
            Console.WriteLine("-c (create: Creates a wad, based on files in the specified directory) [directory containing files to put in wad]\n");
            Console.WriteLine("-d (diff: Compares two wads, and lists different files) [wad to compare] {Optional: directory to extract different files to}");
            Console.WriteLine("-w2z (wad2zip: Converts a wad to a zip) [output zip]");
            Console.WriteLine("-z2w (zip2wad: Converts a zip to a wad) [output wad]");

            Console.WriteLine("eg: www.exe root.wad -x * extracted-root");
            Console.WriteLine("The above command will extract all files from root.wad, into the extracted-root folder");

            //Console.ReadLine();
            Environment.Exit(0);
        }
    }
}
