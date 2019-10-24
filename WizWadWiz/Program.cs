//Wizard101 Wad Wizard V1.0
//You know you love my program names :P
//
//This tool can be used to read/extract/modify/create KingsIsle .wad files
//It has been tested and verified to work with both wizard101 and pirate101
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
//      UPDATE: I don't need to use e pre-determined buffer size. It's dynamically created during the extraction, so this isn't a problem.
//
//  CRC-Collision:
//      diff-checking can fail if the files differ, but the CRC doesn't
//      This also happens if the crc data in the wad is spoofed
//      If KI randomly decided to set all CRC data to 00000000, then diff-checking would fail
//      The workaround for this would be to calculate the checksum myself, rather than relying on the included CRC
//          This takes more time, and would still be vulnerable to crc-collisions
//          To remedy this, I could use a stronger hash function, which could hurt performance even more (although, most suitable hash functions should be fast enough on a modern machine)
//      UPDATE: Hashing is pretty quick. I could also just compare the two byte arrays in-memory, which wouldn't have this issue, and should be faster than hashing anyway.
//
//  ExclusionDirectories:
//      If the extraction directory starts with '..', it means the user is extracting up a directory
//      This means that WWW will still see the directory, but when creating the exclusion, it will add '..' to the directory name.
//      The result of this, is that the exclusion is not applied, and windows defender will consume large amounts of CPU again
//      It could probably be fixed by performing checks to see if the directory contains '..', and if so, add the full path instead of the relative path (eg; including C:\)
//          When the path includes a ':', it will use the full path instead of using a relative path
//          Then when '..' is encountered, remove the parent directory entry (eg; replace C:\dir1\dir2\dir3\..\dir4, with C:\dir1\dir2\dir4)
//      UPDATE: I'm pretty sure this is fixed now (I haven't done much testing. But from the few things I tried, it worked fine)
//
//I don't really know how to verify the checksums included in .wad files
//It's not too important, because my program is pretty safe with extraction, so there shouldn't be any data corruption.
//I'd like to get checksums figured out at some point though, just for that extra bit of safety
//
//UPDATE: I figured out the checksum. It's just for the compressed data, not the extracted data :/
//It's a fairly simple CRC32, just with a couple of non-standard paramters:
//Polynomial: 04c11db7    Initial value: 0    XOR: 0/none    Reflection: input and output
//
//I think the CRC is for verifying the integrity of files that aren't compressed (eg; audio files)
//The compressed files use zlib streams, so there's already an adler32 of the expected output.
//I'm not sure why they don't compress all files, and remove the crc completely. Just seems like a waste of resources when only a few files are left uncompressed.
//
//In the meantime, I can use the checksum for diff-checking!
//That's pretty cool, because we'd be able to diff-check without extracting anything.
//If it's all done in memory, it'll be very fast.
//The other alternative would be to calculate the CRC of the compressed data, which would be slightly slower, but still much faster than diff-checking files on disk
//

using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Collections.Generic;

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
            public byte[] Data;
        }

        public static string ExclusionDir = ""; //Directory to create/remove the windows defender exclusion

        [STAThread]
        static void Main(string[] args)
        {
            //args = new string[4];
            //args[0] = "Mob-WorldData.wad";
            //args[1] = "-x";
            //args[2] = "*";
            //args[3] = "mobout";
            System.Diagnostics.Stopwatch MainTimer = new System.Diagnostics.Stopwatch();
            MainTimer.Start();
            string wad = "";    //wad filename
            string mode = "";   //Operating mode
            string arg1 = "";   //Argument 1 for mode
            string arg2 = "";   //Argument 2 for mode

            bool HadArgs = false; //Whether the user supplied arguments, or just a filename

            //Try grabbing wad name and mode

            try
            {
                wad = args[0];
            }
            catch   //If the wad/mode couldn't be grabbed
            {
                Console.WriteLine("Please specify wad!");    //Print error message
                PrintHelp();    //Print usage info
            }

            try
            {
                mode = args[1];
                HadArgs = true;
            }
            catch   //If the wad/mode couldn't be grabbed
            {
                Console.WriteLine("No mode selected. Defaulting to extract all.");    //Print error message
                mode = "-x";
                //PrintHelp();    //Print usage info
            }

            //If the user specified a mode, try grabbing arguments for specified mode
            if (HadArgs)
            {
                try
                {
                    if (mode == "-x" || mode == "-a")   //If extract/add mode, grab two arguments
                    {
                        arg1 = args[2];
                        arg2 = args[3];
                    }
                    else if (mode == "-r" || mode == "-c" || mode == "-d")  //Remove/Create/Diff mode, one arg
                        arg1 = args[2];
                    else if (mode != "-i" && mode != "-w2z")    //If the mode is not -i (takes no arguments), then we don't know what mode they specified
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
            }
            else
            {
                
                if (!System.IO.File.Exists(wad))
                {
                    Console.WriteLine("Wad file not found!");
                    PrintHelp();
                }

                arg1 = "*"; //Set extraction file-selection to all
                System.Windows.Forms.FolderBrowserDialog fd = new System.Windows.Forms.FolderBrowserDialog();
                fd.Description = "Choose where to save the extracted files";
                MainTimer.Stop();
                if (fd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MainTimer.Start();
                    arg2 = fd.SelectedPath;
                    ExclusionDir = arg2;
                }
                else
                    Quit();
            }

            FileList[] entries = new FileList[0];   //Pre-init the 'entries' array

            if(mode == "-a")    //If using file-add mode
            {
                Console.WriteLine("Not yet implemented");
                Quit();
                //Should I add an argument to indicate whether to add a file, or read a filelist?
                //Or should I take the entire list as a single string?               
            }

            if(mode == "-c")    //Create (wad) mode
            {
                
                //Make sure the input directory exists
                if (!Directory.Exists(arg1))
                {
                    Console.WriteLine("Input directory not found!");
                    PrintHelp();
                }

                string[] InFiles = Directory.GetFiles(arg1, "*.*", SearchOption.AllDirectories);    //Grab a list of all files within the directory
                entries = new FileList[InFiles.Count()];    //Make a new FileList for storing these files in the wad
                Console.WriteLine("Filecount: {0}", InFiles.Count());   //Debug

                //For each file
                Parallel.For(0, InFiles.Count(), i =>
                {
                    entries[i].Filename = InFiles[i].Substring(arg1.Length, InFiles[i].Length - arg1.Length);   //Remove directory info that shouldn't be included in the wad (path up to the wad contents)
                    if(entries[i].Filename.IndexOf("\\") == 0)  //If the entry starts with a \
                        entries[i].Filename = entries[i].Filename.Substring(1, entries[i].Filename.Length - 1); //Get rid of the slash

                    //MemoryStream ms = new MemoryStream(File.ReadAllBytes(InFiles[i]));  //Read
                    entries[i].Data = File.ReadAllBytes(InFiles[i]);    //Read the file into memory
                    entries[i].Size = (uint)entries[i].Data.Length;
                    //Console.WriteLine("{0}, with a size of {1}, was read to memory", entries[i].Filename, entries[i].Size);
                });

                Console.WriteLine("Files read to memory");

                List<byte> WadHeader = new List<byte>();
                List<byte> WadBody = new List<byte>();

                var crcparam = new CrcSharp.CrcParameters(32, 0x04c11db7, 0, 0, true, true);
                var crc = new CrcSharp.Crc(crcparam);

                for (int i = 0; i < entries.Length; i++)
                {
                    entries[i].Offset = (uint)WadBody.Count();  //Save the offset for this entry

                    MemoryStream Compressed = new MemoryStream();
                    Ionic.Zlib.ZlibStream zls = new Ionic.Zlib.ZlibStream(Compressed, Ionic.Zlib.CompressionMode.Compress, Ionic.Zlib.CompressionLevel.BestCompression, false);
                    zls.Write(entries[i].Data, 0, entries[i].Data.Length);
                    zls.Close();
                    byte[] compdata = Compressed.ToArray();
                    WadBody.AddRange(compdata);   //Add the compressed data to the body of the wad
                    entries[i].CRC = (uint)crc.CalculateAsNumeric(entries[i].Data); //Add the CRC of the compressed data (idk why they do this)
                    entries[i].CompressedSize = (uint)compdata.Length;    //Save the size of the compressed data
                    //Console.WriteLine("CRC: {0} : {1}", entries[i].CRC.ToString("X8"), entries[i].Filename);    //Debug
                }
                Console.WriteLine("Wad body added");
                //Add wad header info
                WadHeader.AddRange(new byte[] { 0x4B, 0x49, 0x57, 0x41, 0x44, 0x02, 0x00, 0x00, 0x00}); //Add magic and wad version (2)
                WadHeader.AddRange(BitConverter.GetBytes(entries.Length));  //Add the file count
                WadHeader.Add(0x01);    //Add wad2 byte (idk what it is, but it's required)

                int[] offoff = new int[entries.Length];   //Keeps track of the offset for the 'Offset' field in the header (we can't know the offset until the header is finished, so we add it later)

                //Add each file to the wad header
                for (int i = 0; i < entries.Length; i++)
                {
                    offoff[i] = WadHeader.Count();  //Save current offset, so that we can update the 'offset' field when the header is finished
                    WadHeader.AddRange(new byte[] { 0x00, 0x00, 0x00, 0x00 });  //Add filler offset
                    WadHeader.AddRange(BitConverter.GetBytes(entries[i].Size)); //Add uncompresses file size
                    WadHeader.AddRange(BitConverter.GetBytes(entries[i].CompressedSize));   //Add compressed file size
                    WadHeader.Add(0x01);    //Set 'IsCompressed' to 1 (we compress all files, so this is always true)
                    WadHeader.AddRange(BitConverter.GetBytes(entries[i].CRC));  //Add the compressed-data crc (idk why they have this, because the extracted file has a crc anyway)
                    WadHeader.AddRange(BitConverter.GetBytes(entries[i].Filename.Length+1));  //Add filename length
                    WadHeader.AddRange(ASCIIEncoding.ASCII.GetBytes(entries[i].Filename));  //Add the filename
                    WadHeader.Add(0x00);    //Add padding
                }
                Console.WriteLine("Wad Header added");
                //Fix the offsets
                for(int i = 0; i < entries.Length; i++)
                {
                    byte[] offsetbytes = BitConverter.GetBytes(WadHeader.Count() + entries[i].Offset);
                    WadHeader[offoff[i]] = offsetbytes[0];
                    WadHeader[offoff[i] + 1] = offsetbytes[1];
                    WadHeader[offoff[i] + 2] = offsetbytes[2];
                    WadHeader[offoff[i] + 3] = offsetbytes[3];
                }
                Console.WriteLine("Offsets fixed");
                byte[] Output = WadHeader.Concat(WadBody).ToArray();
                File.WriteAllBytes(wad, Output);  //Save 
                Console.WriteLine("Wad Saved");
                Quit();
            }

            if (mode != "-c")    //If the tool is not in create mode, check if the wad exists
            {
                if (!System.IO.File.Exists(wad))
                {
                    Console.WriteLine("Wad file not found!");
                    PrintHelp();
                }
                else    //If the file exists
                {
                    Console.WriteLine("Reading wad to memory...");
                    entries = ReadWad(wad);    //Read the wad into the 'entries' array
                }
            }

            if(mode == "-d" && !File.Exists(arg1))  //If using diff mode, and the second file doesn't exist
            {
                Console.WriteLine("Second wad does not exist!");
                PrintHelp();
            }

            if(mode == "-w2z")  //If using Wad2Zip mode
            {
                //Stopwatch for diagnostics
                System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                stopwatch.Start();

                Console.WriteLine("Calculating CRC's...");

                //For each file in the wad; extract it in-memory, calculate the CRC of the extracted data, and update the entry's CRC field (because this is done in-memory, it should be less than a couple of seconds)
                Parallel.For(0, entries.Length, i =>
                {
                    byte[] filemem = new byte[0];

                    if (entries[i].IsCompressed)   //If the file is marked as compressed
                    {
                        filemem = Ionic.Zlib.ZlibStream.UncompressBuffer(entries[i].Data);
                        Array.Copy(entries[i].Data, 2, entries[i].Data, 0, entries[i].Data.Length - 6);
                    }
                    else    //If the file isn't compressed
                        filemem = entries[i].Data;


                    Ionic.Crc.CRC32 crc = new Ionic.Crc.CRC32();
                    entries[i].CRC = (uint)crc.GetCrc32(new MemoryStream(filemem)); //Replace the entries' crc with the CRC of the compressed data (KI are shit and use their own incompatible polynomials for their checksum, so we need to recalculate it with a standard polynomial)
                });
                stopwatch.Stop();

                //Stopwatch for timing the zip-process
                System.Diagnostics.Stopwatch ziptimer = new System.Diagnostics.Stopwatch();
                ziptimer.Start();
                byte[] outputfile = Zipper(entries);    //Add all file entries to a zip in-memory, and return the zip
                ziptimer.Stop();

                File.WriteAllBytes(wad + ".zip", outputfile);   //Save the created zip to disk (input filename with .zip appended)

                MainTimer.Stop();

                Console.WriteLine("Updated CRC's in {0} Seconds", stopwatch.Elapsed.TotalSeconds);
                Console.WriteLine("Zipped in {0} Seconds", ziptimer.Elapsed.TotalSeconds);
                Console.WriteLine("Total program runtime: {0} Seconds", MainTimer.Elapsed.TotalSeconds);
                Quit(); //Exit
            }


            if (mode == "-i" || mode == "-x" || mode == "-d")
            {

                if (mode == "-i")   //If using info mode
                {
                    StringBuilder sb = new StringBuilder(); //Make a new stringbuilder (stringbuilder is much faster than just appending strings normally)
                    for (int i = 0; i < entries.Length; i++) //For each file entry
                    {
                        sb.AppendLine(entries[i].Offset.ToString("X") + ":" + entries[i].CompressedSize + ":" + entries[i].Size + ":" + entries[i].Filename);   //Add a newline to the output string, with the offset and filename (offset:filename)
                    }
                    Console.WriteLine(sb);  //Print the output string
                    MainTimer.Stop();
                    Console.WriteLine("{0} files found in {1} Seconds", entries.Length, MainTimer.Elapsed.TotalSeconds);
                    Quit();    //Quit
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
                            arg2 = ResolveDir(arg2);

                            extract = true; //Enable file-extraction
                        }
                        catch   //If something went wrong whlie creating the directory
                        {
                            Console.WriteLine("Error creating directory: {0}\nMaybe you're trying to write to a folder you don't have permission to write in?", arg2);  //Let the user know something went wrong
                            Quit();    //Exit
                        }
                    }
                    catch   //If there isn't an output-folder argument
                    {
                        Console.WriteLine("No output folder specified. No extraction will be performed.");  //Print a message stating operating mode
                    }

                    Console.WriteLine("Checking differences...");

                    FileList[] entries2 = ReadWad(arg1);
                    bool[] ExtractIt = new bool[entries2.Length];    //Create a bool array, which keeps track of which files to extract from the second wad

                    StringBuilder MissingIn1 = new StringBuilder();
                    StringBuilder MissingIn2 = new StringBuilder();
                    StringBuilder DiffIn2 = new StringBuilder();
                    object threadsync = new object();   //Threadsync is used to prevent threads from overwriting eachothers data

                    //Check what files are missing
                    Parallel.For(0, entries.Length, i =>    //For each file entry in the first wad
                    { 
                        bool Exists = false;    //Mark the file as not existing in both wads (default, until proven otherwise)

                        for (int j = 0; j < entries2.Length; j++)  //For each file in second wad
                        {
                            if (entries2[j].Filename == entries[i].Filename)  //If the currently-processes filename in wad 1 is also the same filename in wad2
                            {
                                Exists = true;  //Mark the file as existing in both

                                if (entries2[j].CRC != entries[i].CRC)  //If the files have a different CRC
                                {
                                    lock (threadsync) //Use a lock to prevent SB from getting corrupt by multiple-accesses
                                    {
                                        DiffIn2.AppendLine(entries[i].Filename);    //Add the filename to the 'DiffIn2' sb, so we can print the results later
                                    }
                                    ExtractIt[j] = true;    //Mark the file in wad2 for extraction, because it's different

                                }

                                break;  //Stop searching for this file, because we've already checked it
                            }
                        }
                        if (!Exists)    //If the file wasn't marked as existing
                        {
                            lock (threadsync) //Use a lock to prevent SB from getting corrupt by multiple-accesses
                            {
                                MissingIn2.AppendLine(entries[i].Filename); //Add the file to the 'MissingIn2' sb for printing later
                            }
                        }
                    });


                    //Check what files exist in both wads (and whether any files were added)
                    Parallel.For(0, entries2.Length, i =>  
                    {
                        bool Exists = false;
                        for (int j = 0; j < entries.Length; j++)
                        {
                            if (entries2[i].Filename == entries[j].Filename)
                            {
                                Exists = true;  //Mark the file as existing in both
                                break;  //Stop searching for this file
                            }
                        }
                        if (!Exists)    //If the file wasn't marked as existing
                        {
                            lock(threadsync)  //Use a lock to prevent SB from getting corrupt by multiple-accesses
                            {
                                MissingIn1.AppendLine(entries[i].Filename); //Add the file to the 'MissingIn1' sb for printing later
                            }
                            ExtractIt[i] = true;    //Mark the file in wad2 for extraction, because it doesn't exist in wad1
                        }
                    });

                    Console.WriteLine("Diff-checking complete!");
                    if (extract)    //If the user specified an output directory (extract diff files)
                    {
                        Console.WriteLine("Extracting new/different files..."); //Inform the user that the files will now be extracted

                        Console.WriteLine("Pre-creating directories...");
                        for (int i = 0; i < entries2.Length; i++)    //For each file in the second wad
                        {
                            if (ExtractIt[i])   //If the file is marked for extraction, check it for any subdirectories, and create them if necessary
                                PreCreate(entries2[i], arg2);    //Call 'PreCreate' for that file, and any required subdirs for that file will be created
                        }

                        Console.WriteLine("Directories created!\nExtracting...");

                        //For each file in the second wad, check if it's marked for extraction; and if so, extract it.
                        Parallel.For(0, entries2.Length, i =>
                        {
                            if (ExtractIt[i])   //If the file was marked for extraction (new/different file)
                            {
                                if (entries2[i].IsCompressed)   //If the file is marked as compressed
                                    entries2[i].Data = Ionic.Zlib.ZlibStream.UncompressBuffer(entries2[i].Data);    //Decompress the file


                                using (FileStream output = new FileStream(arg2 + "\\" + entries2[i].Filename, FileMode.Create))    //Create the file that is being extracted (replaces old files if they exist)
                                {
                                    output.Write(entries2[i].Data, 0, entries2[i].Data.Length);   //Write the file from memory to disk
                                }
                            }
                        });

                    }



                    if (MissingIn1.Length > 0 || MissingIn2.Length > 0 || DiffIn2.Length > 0)   //If there were any file differences
                    {
                        Console.WriteLine("Differences found!");
                        
                        if (entries.Length != entries2.Length)
                            Console.WriteLine("------------------------------File count is different!------------------------------\nOld:{0}\tNew:{1}", entries.Length, entries2.Length);
                        
                        if (MissingIn1.Length > 0)
                            Console.WriteLine("------------------------------Files missing in first wad------------------------------\n{0}", MissingIn1);
                        
                        if (MissingIn2.Length > 0)
                            Console.WriteLine("------------------------------Files missing in second wad------------------------------\n{0}", MissingIn2);
                        
                        if (DiffIn2.Length > 0)
                            Console.WriteLine("------------------------------Files changed------------------------------\n{0}", DiffIn2);
                    }
                    else
                        Console.WriteLine("No differences found!");

                    MainTimer.Stop();
                    Console.WriteLine("Total program runtime: {0} Seconds", MainTimer.Elapsed.TotalSeconds);
                    Quit();
                }
                else if (mode == "-x")   //If using extract mode
                {
                    arg2 = ResolveDir(arg2);
                    if (arg1 != "*")   //If the user specified a file to extract (not all files)
                    {
                        for(int i = 0; i < entries.Length; i++)  //For each file in the filelist
                        {
                            if (string.Equals(entries[i].Filename,arg1,StringComparison.OrdinalIgnoreCase) || string.Equals(entries[i].Filename.Replace('/', '\\'), arg1, StringComparison.OrdinalIgnoreCase))   //If the file entry matches the user-specified file (ignoring case and slash-direction)
                            {
                                Console.WriteLine("File found!");

                                PreCreate(entries[i], arg2);    //Check if file is located in a subdirectory. If so, create the appropriate directory structure

                                if (entries[i].IsCompressed)   //If the file is marked as compressed
                                    entries[i].Data = Ionic.Zlib.ZlibStream.UncompressBuffer(entries[i].Data);  //Decompress the data
                                    
                                using (FileStream output = new FileStream(arg2 + "\\" + entries[i].Filename, FileMode.Create))    //Create the file that is being extracted (replaces old files if they exist)
                                {
                                    output.Write(entries[i].Data, 0, entries[i].Data.Length);   //Write the file from memory to disk
                                    Console.WriteLine("File extracted to: {0}", arg2 + "\\" + entries[i].Filename.Replace('/','\\'));
                                }

                                Quit();
                            }
                            //If the filename doesn't match, read the next entry
                        }
                        //If all files have been scanned, and the user-specified file wasn't found; let the user know, and give them some advice
                        Console.WriteLine("'{0}' was not found in the specified wad!",arg1);
                        Console.WriteLine("Make sure you include the file's parent directories");
                        Console.WriteLine("eg: capabilities\\cpu.xml");
                        Quit();
                    }
                    else    //If the file is '*": Don't 'really' need to say else here, because it would have exited previously anyway, but it just makes things more readable
                    {
                        
                        //Create directories in advance, using a single thread (prevents race crash when parallel threads attempt to create the same directory at the same time)
                        Console.WriteLine("Pre-creating directories...");
                        for (int i = 0; i < entries.Length; i++)
                            PreCreate(entries[i], arg2);    //Pre-create any subdirectories listed in the filename (arg2 is the base directory for extraction)
                        Console.WriteLine("Directories created!\nExtracting...");

                        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();
                        stopwatch.Start();

                        Parallel.For(0,entries.Length, i =>
                        {
                            if (entries[i].IsCompressed)   //If the file is marked as compressed
                                entries[i].Data = Ionic.Zlib.ZlibStream.UncompressBuffer(entries[i].Data);
                        });

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
                        Console.WriteLine("Extracted {0} files in {1} Seconds", entries.Length, stopwatch.Elapsed.TotalSeconds);
                        Console.WriteLine("Wrote files in {0} Ms", writetimer.ElapsedMilliseconds);
                        Console.WriteLine("Total program runtime: {0} seconds", MainTimer.Elapsed.TotalSeconds);
                        Quit(); //Exit
                    }
                }
            }
        }

        //Hmmm... I wonder what this does
        static void PrintHelp()
        {
            Console.WriteLine("Usage:");
            Console.WriteLine("www.exe [wad] [mode] [arguments]\n");
            Console.WriteLine("eg: www.exe root.wad -x * extracted-root");
            Console.WriteLine("The above command will extract all files from root.wad, into the extracted-root folder\n");
            Console.WriteLine("Modes:");
            Console.WriteLine("-i (info: prints info about contained info)");
            Console.WriteLine("-x (extract) [filename (* for all files)] [extraction directory]");
            //Console.WriteLine("-a (add: Add a file to the wad) [file to insert] [directory\\name inside wad]");
            //Console.WriteLine("-r (remove: Removes a file from the wad) [directory\\name of file to remove]");
            Console.WriteLine("-c (create: Creates a wad) [directory containing files to put in wad]");
            Console.WriteLine("-d (diff: Compares two wads, and lists different files) [wad to compare] {Optional: extraction directory}");
            Console.WriteLine("-w2z (wad2zip: Converts a wad to a zip) [output zip]");
            //Console.WriteLine("-z2w (zip2wad: Converts a zip to a wad) [output wad]");

            Quit();
        }

        //Creates a windows defender directory exclusion on the output folder (significantly boosts extraction speeds)
        static void CreateExclusion()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "powershell.exe";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Arguments = "-inputformat none -outputformat none -NonInteractive -Command Add-MpPreference -ExclusionPath \"" + ExclusionDir + "\"";
            proc.Start();
        }

        //Removes the windows defender directory exclusion on the output folder (we don't want to make any permanent changes to the user's system)
        static void RemoveExclusion()
        {
            Process proc = new Process();
            proc.StartInfo.FileName = "powershell.exe";
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;
            proc.StartInfo.Arguments = "-inputformat none -outputformat none -NonInteractive -Command Remove-MpPreference -ExclusionPath \"" + ExclusionDir + "\"";
            proc.Start();
        }


        //Used in replacement of Environment.Exit, ensures that the defender exclusion is removed before quitting
        static void Quit()
        {
            if (IsElevated())   //If the program is elevated (meaning the directory exclusion would have been installed)
                RemoveExclusion();   //Remove windows defender exclusion
            Environment.Exit(0);
        }

        // Check if the program is running with administrator privileges
        public static bool IsElevated()
        {
            return WindowsIdentity.GetCurrent().Owner.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid);   //Dirty one-liner that checks if the program is using the permissions of the administrator account
            //This grabs the current security token, and compares it with the BuiltInAdministrator security token. If they match, the process has admin privs.
            //There might be some edge-cases where this will fail to evaluate correctly. I guess we'll see
        }

        //Method for pre-creating directories for extraction
        public static void PreCreate(FileList entry, string BaseDir)  //entry: File to create dir for   //BaseDir: Base directory to create subdirs in
        {
            if (entry.Filename.Contains('\\') || entry.Filename.Contains('/'))  //If the filename contains a directory
            {
                int slashindex = entry.Filename.LastIndexOf('\\'); //Grab the last \ in the filename (grab the last subdirectory directory)
                if (slashindex < 0) //If there wasn't a \ in the filename
                    slashindex = entry.Filename.LastIndexOf('/');   //Grab the last / instead

                if (!Directory.Exists(BaseDir + "\\" + entry.Filename.Substring(0, slashindex)))  //If the directory\subdirectory doesn't exist
                {
                    Directory.CreateDirectory(BaseDir + "\\" + entry.Filename.Substring(0, slashindex));    //Create the directory\subdirectory
                }
            }
        }

        //Resolve directories properly (also helps ensure that the defender exclusion is applied to the correct folder)
        public static string ResolveDir(string arg2)
        {
            if (!arg2.Contains(':'))    //If the supplied directory does not contain a ':'
                arg2 = Directory.GetCurrentDirectory() + "\\" + arg2;   //Assume that the folder is based in the working directory

            arg2 = Path.GetFullPath(arg2);

            if (!Directory.Exists(arg2))    //If the output directory doesn't exist
                Directory.CreateDirectory(arg2);    //Create the output directory

            ExclusionDir = arg2;    //Set the directory used for the WindowsDefender exlusion to the extraction directory

            if (IsElevated())    //If the user is running this process with admin privs
                CreateExclusion();   //Create a windows defender exclusion for the extraction directory

            return arg2;
        }

    }
}
