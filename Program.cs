using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Security.Cryptography;

namespace DuplicateDestroyer
{
    class Program
    {
        static private List<string> md5hashes;
        static bool filenamemode;
        static private bool remove;

        static void Main(string[] args)
        {
            Console.WriteLine("Duplicate Destroyer");
            Console.WriteLine("'Boring Blackjack'");
            Console.WriteLine("Licenced under Tiny Driplet Licence (can be found at cloudchiller.net)");
            Console.WriteLine("Copyright, Copydrunk, Copypone (c) 2012, Cloud Chiller");
            Console.WriteLine();

            if (args.Contains<string>("-h"))
            {
                Console.WriteLine("HELP:");
                Console.WriteLine("-h       Show this help text.");
                Console.WriteLine("-ok      Safety disable. If omitted, will run in read-only mode.");
                Console.WriteLine("-fn      Work by filenames (w/o extentions) instead of hashes.");
                Console.WriteLine();

                Environment.Exit(0);
            }
                    

                    Console.Write("Counting files. ");
                    int c = CountFiles(System.IO.Directory.GetCurrentDirectory());
                    Console.WriteLine(c + " files found.");
                    md5hashes = new List<string>(c);

                if (args.Contains<string>("-ok"))
                    {
                        remove = true;
                        Console.WriteLine("Duplicate Destroyer is armed and dangerous. Files will be removed.");
                    }
                    else
                    {
                        remove = false;
                        Console.WriteLine("Duplicate Destroyer is running in read-only mode.");
                        Console.WriteLine("The files will only be checked, nothing will be removed.");
                        Console.WriteLine("To actually remove the files, run the program with the argument: -ok");
                    }
                Console.WriteLine();

                    if (args.Contains<string>("-fn"))
                    {
                        filenamemode = true;
                        Console.WriteLine("Looking through the directory for duplicates using filenames.");
                    }
                    else
                    {
                        filenamemode = false;
                        Console.WriteLine("Looking through the directory for duplicates using hashes.");
                    }
                    Console.WriteLine();

                    DirSearch(System.IO.Directory.GetCurrentDirectory());
            
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
            Environment.Exit(0);
        }

        static void CheckFile(ref MD5CryptoServiceProvider mscp, string file)
        {
            string md5b64;

            using (FileStream stream = File.OpenRead(file))
            {
                byte[] filebytes = new byte[stream.Length + 1];
                stream.Read(filebytes, 0, Convert.ToInt32(stream.Length));
                byte[] md5bytes = mscp.ComputeHash(filebytes);
                md5b64 = Convert.ToBase64String(md5bytes);
            }

            if (md5hashes.Contains(md5b64))
            {
                Console.Write("File " + Path.GetFileName(file) + " (hash: " + md5b64 + ") is a duplicate.");

                    try
                    {
                        if ( remove == true )
                        {
                        File.Delete(file);
                        Console.WriteLine(" Deleted.");
                        }
                        else if ( remove == false )
                        {
                            Console.WriteLine();
                        }
                    }
                    catch (System.IO.IOException ex)
                    {
                        Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                    }
                
            }
            else
            {
                md5hashes.Add(md5b64);
            }
        }

        static void CheckFilename(string file)
        {
            string filename = Path.GetFileNameWithoutExtension(file).ToLower();

            if (md5hashes.Contains(filename))
            {
                Console.Write("File " + Path.GetFileName(file) + " is a duplicate.");

                    try
                    {
                        if ( remove == true )
                        {

                        File.Delete(file);
                        Console.WriteLine(" Deleted.");
                        }
                        else if ( remove == false )
                        {
                            Console.WriteLine();
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                    }
                
            }
            else
            {
                md5hashes.Add(filename);
            }
        }

        static void DirSearch(string sDir)
        {
            MD5CryptoServiceProvider mcsp = new MD5CryptoServiceProvider();
            
            try
            {
                    foreach (string f in Directory.GetFiles(sDir, "*", SearchOption.AllDirectories))
                    {
                        if (filenamemode == true)
                        {
                            CheckFilename(f);
                        }
                        else if (filenamemode == false)
                        {
                            CheckFile(ref mcsp, f);
                        }
                    }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to check folder. An exception happened: " + ex.Message);
            }
        }

        static int CountFiles(string sDir)
        {
            int c = 0;
            try
            {
                foreach (string f in Directory.GetFiles(sDir, "*", SearchOption.AllDirectories))
                {
                    c++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to count files. An exception happened: " + ex.Message);
            }
            return c;
        }
    }
}
