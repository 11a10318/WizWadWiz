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

        //Method for pre-creating directories for extraction
        public static void PreCreate(FileList entry, string BaseDir)  //entry: File to create dir for   //BaseDir: Base directory to create subdirs in
        {
            if (entry.Filename.Contains('\\') || entry.Filename.Contains('/'))  //If the filename contains a directory
            {
                int slashindex = entry.Filename.LastIndexOf('\\'); //Grab the last \ in the filename (grab the last subdirectory directory)
                if (slashindex < 0) //If there wasn't a \ in the filename
                    slashindex = entry.Filename.LastIndexOf('/');   //Grab the last / instead

                if (!Directory.Exists(entry.Filename.Substring(0, slashindex)))  //If the directory\subdirectory doesn't exist
                {
                    Directory.CreateDirectory(BaseDir + "\\" + entry.Filename.Substring(0, slashindex));    //Create the directory\subdirectory
                }
            }
        }

    }
}
