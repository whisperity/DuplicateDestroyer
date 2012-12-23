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
        static private string[] derpy;
        static int currentcount = 0;
        static bool filenamemode = false;

        static void Main(string[] args)
        {
            Console.WriteLine("Duplicate Destroyer");
            Console.WriteLine("'Boring Blackjack'");
			Console.WriteLine("Licenced under Tiny Driplet Licence (can be found at cloudchiller.net)");
            Console.WriteLine("Copyright, Copydrunk, Copypone (c) 2012, Cloud Chiller");
            Console.WriteLine();
            if (args.Length > 0)
            {
                if (args[0] == "-ok")
                {
                    if (args.Contains<string>("-fn"))
                    {
                        filenamemode = true;
                    }
                    Console.Write("Counting files. ");
                    int c = CountFiles(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
                    Console.WriteLine(c + " files found.");
                    derpy = new string[c];
                    if (filenamemode == false)
                    {
                        Console.WriteLine("Looking through the directory for duplicates.");
                    }
                    else
                    {
                        Console.WriteLine("Looking through the directory for duplicates using filenames.");
                    }
                    DirSearch(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location));
                }
            }
            else
            {
                Console.WriteLine("HELP:");
                Console.WriteLine("-ok      Safety disable, Has to be first argument.");
                Console.WriteLine("-fn      Work by filenames (w/o extentions) instead of hashes.");
                Console.WriteLine();
            }
            Console.WriteLine("Press any key to continue.");
            Console.ReadKey();
        }

        static void DirSearch(string sDir)
        {
            MD5CryptoServiceProvider mcsp = new MD5CryptoServiceProvider();
            try
            {
                if (filenamemode == false)
                {
                    foreach (string f in Directory.GetFiles(sDir))
                    {
                        FileStream file = File.OpenRead(f);
                        byte[] exebytes = new byte[file.Length + 1];
                        file.Read(exebytes, 0, Convert.ToInt32(file.Length));
                        byte[] md5bytes = mcsp.ComputeHash(exebytes);
                        string checkthesedubs = Convert.ToBase64String(md5bytes);
                        if (derpy.Contains<string>(checkthesedubs))
                        {
                            Console.Write("File " + Path.GetFileName(f) + ", MD5 Base64 " + checkthesedubs + " is a duplicate.");
                            try
                            {
                                file.Close();
                                File.Delete(f);
                                Console.WriteLine(" Deleted.");
                            }
                            catch (System.Exception except)
                            {
                                Console.WriteLine(" ERROR: " + except.Message);
                            }
                        }
                        else
                        {
                            derpy[currentcount] = checkthesedubs;
                            currentcount++;
                        }
                    }

                    foreach (string d in Directory.GetDirectories(sDir))
                    {
                        foreach (string f in Directory.GetFiles(d))
                        {
                            FileStream file = File.OpenRead(f);
                            byte[] exebytes = new byte[file.Length + 1];
                            file.Read(exebytes, 0, Convert.ToInt32(file.Length));
                            byte[] md5bytes = mcsp.ComputeHash(exebytes);
                            string checkthesedubs = Convert.ToBase64String(md5bytes);
                            if (derpy.Contains<string>(checkthesedubs))
                            {
                                Console.Write("File " + Path.GetFileName(f) + ", MD5 Base64 " + checkthesedubs + " is a duplicate.");
                                try
                                {
                                    File.Delete(f);
                                    Console.WriteLine(" Deleted.");
                                }
                                catch (System.Exception except)
                                {
                                    Console.WriteLine(" ERROR: " + except.Message);
                                }
                            }
                            else
                            {
                                derpy[currentcount] = checkthesedubs;
                                currentcount++;
                            }
                        }
                        DirSearch(d);
                    }
                }
                else
                {
                    foreach (string f in Directory.GetFiles(sDir))
                    {
                        string checkthesedubs = Path.GetFileNameWithoutExtension(f);
                        checkthesedubs = checkthesedubs.ToLower();
                        if (derpy.Contains<string>(checkthesedubs))
                        {
                            Console.Write("File " + Path.GetFileName(f) + " is a duplicate.");
                            try
                            {
                                File.Delete(f);
                                Console.WriteLine(" Deleted.");
                            }
                            catch (System.Exception except)
                            {
                                Console.WriteLine(" ERROR: " + except.Message);
                            }
                        }
                        else
                        {
                            derpy[currentcount] = checkthesedubs;
                            currentcount++;
                        }
                    }

                    foreach (string d in Directory.GetDirectories(sDir))
                    {
                        foreach (string f in Directory.GetFiles(d))
                        {
                            string checkthesedubs = Path.GetFileNameWithoutExtension(f);
                            checkthesedubs = checkthesedubs.ToLower();
                            if (derpy.Contains<string>(checkthesedubs))
                            {
                                Console.Write("File " + Path.GetFileName(f) + " is a duplicate.");
                                try
                                {
                                    File.Delete(f);
                                    Console.WriteLine(" Deleted.");
                                }
                                catch (System.Exception except)
                                {
                                    Console.WriteLine(" ERROR: " + except.Message);
                                }
                            }
                            else
                            {
                                derpy[currentcount] = checkthesedubs;
                                currentcount++;
                            }
                        }
                        DirSearch(d);
                    }
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
        }

        static int CountFiles(string sDir)
        {
            int c = 0;
            try
            {
                foreach (string f in Directory.GetFiles(sDir))
                {
                    c++;
                }

                foreach (string d in Directory.GetDirectories(sDir))
                {
                    foreach (string f in Directory.GetFiles(d))
                    {
                        c++;
                    }
                    DirSearch(d);
                }
            }
            catch (System.Exception excpt)
            {
                Console.WriteLine(excpt.Message);
            }
            return c;
        }
    }
}
