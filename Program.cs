using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace DuplicateDestroyer
{
    static class Program
    {
        static private List<string> MD5Hashes;
        static private bool UseFilenames;
        static private bool ShouldRemove;
        static private bool FileRemoveException;

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

            Console.Write("Counting files... ");
            int c = CountFiles(System.IO.Directory.GetCurrentDirectory());
            Console.WriteLine(c + " files found.");
            MD5Hashes = new List<string>(c);

            if (args.Contains<string>("-ok"))
            {
                ShouldRemove = true;
                Console.WriteLine("Duplicate Destroyer is armed and dangerous. Files will be removed.");
            }
            else
            {
                ShouldRemove = false;
                Console.WriteLine("Duplicate Destroyer is running in read-only mode.");
                Console.WriteLine("The files will only be checked, nothing will be removed.");
                Console.WriteLine("To actually remove the files, run the program with the argument: -ok");
            }
            Console.WriteLine();

            if (args.Contains<string>("-fn"))
            {
                UseFilenames = true;
                Console.WriteLine("Looking through the directory for duplicates using filenames.");
            }
            else
            {
                UseFilenames = false;
                Console.WriteLine("Looking through the directory for duplicates using hashes.");
            }
            Console.WriteLine();

            // Actual work happens here.
            FileRemoveException = false;
            DirSearch(System.IO.Directory.GetCurrentDirectory());

            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();

            if (FileRemoveException == true)
            {
                Environment.Exit(2);
            }
            else if (FileRemoveException == false)
            {
                Environment.Exit(0);
            }
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

            if (MD5Hashes.Contains(md5b64))
            {
                Console.Write("File " + Path.GetFileName(file) + " (hash: " + md5b64 + ") is a duplicate.");

                try
                {
                    if (ShouldRemove == true)
                    {
                        File.Delete(file);
                        Console.WriteLine(" Deleted.");
                    }
                    else if (ShouldRemove == false)
                    {
                        Console.WriteLine();
                    }
                }
                catch (System.IO.IOException ex)
                {
                    Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                    FileRemoveException = true;
                }
            }
            else
            {
                MD5Hashes.Add(md5b64);
            }
        }

        static void CheckFilename(string file)
        {
            string filename = Path.GetFileNameWithoutExtension(file).ToLower();

            if (MD5Hashes.Contains(filename))
            {
                Console.Write("File " + Path.GetFileName(file) + " is a duplicate.");

                try
                {
                    if (ShouldRemove == true)
                    {

                        File.Delete(file);
                        Console.WriteLine(" Deleted.");
                    }
                    else if (ShouldRemove == false)
                    {
                        Console.WriteLine();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(" ERROR: Unable to delete. An exception happened: " + ex.Message);
                    FileRemoveException = true;
                }
            }
            else
            {
                MD5Hashes.Add(filename);
            }
        }

        static void DirSearch(string directory)
        {
            MD5CryptoServiceProvider mcsp = new MD5CryptoServiceProvider();

            try
            {
                foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    if (UseFilenames == true)
                    {
                        CheckFilename(f);
                    }
                    else if (UseFilenames == false)
                    {
                        CheckFile(ref mcsp, f);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to check folder. An exception happened: " + ex.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
        }

        static int CountFiles(string directory)
        {
            int c = 0;
            try
            {
                foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    c++;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Unable to count files. An exception happened: " + ex.Message);
                Console.WriteLine("Press any key to exit...");
                Console.ReadKey();
                Environment.Exit(1);
            }
            return c;
        }
    }
}