using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DuplicateDestroyer
{
    static class Program
    {
        static private Dictionary<string, string> Files;
        static private bool ShouldRemove;
        static private bool FileRemoveException;
        static private bool Verbose;
        static bool AutoOldest;
        static bool AutoNewest;

        static void Main(string[] args)
        {
            FileRemoveException = false;

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
                Console.WriteLine("-v       Verbose mode");
                Console.WriteLine("-o       Automatically schedule the OLDEST file for keeping.");
                Console.WriteLine("-n       Automatically schedule the NEWEST file for keeping.");
                Console.WriteLine();
                Console.WriteLine("Omitting both -o and -n results in the user being queried about which file to keep.");
                Console.WriteLine("Using both -o and -n with each other is an error.");
                Console.WriteLine();

                Environment.Exit(0);
            }

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

            if (args.Contains<string>("-v"))
            {
                Verbose = true;
            }
            else
            {
                Verbose = false;
            }
            
            if (args.Contains<string>("-o"))
            {
                AutoOldest = true;
            }
            else
            {
                AutoOldest = false;
            }

            if (args.Contains<string>("-n"))
            {
                AutoNewest = true;
            }
            else
            {
                AutoNewest = false;
            }

            if (AutoOldest == true && AutoNewest == true)
            {
                Console.WriteLine("ERROR: Conflicting arguments.");
                Console.WriteLine("It's an error to use -o and -n together.");
                Console.WriteLine();

                Environment.Exit(3);
            }

            Console.Write("Counting files... ");
            int c = CountFiles(Directory.GetCurrentDirectory());
            Console.WriteLine(c + " files found.");
            Files = new Dictionary<string, string>(c);
            Console.WriteLine();

            DirSearch(System.IO.Directory.GetCurrentDirectory());
            Console.WriteLine();

            SortedList<string,List<string>> DuplicateLists;
            AnalyzeFilelist(out DuplicateLists);
            Console.WriteLine();

            SortedList<string, List<string>> RemoveLists = new SortedList<string, List<string>>(DuplicateLists.Count);
            foreach (List<string> duplicate_list in DuplicateLists.Values)
            {
                SortedList<DateTime, string> duplicate_ordered = new SortedList<DateTime, string>(duplicate_list.Count);

                foreach (string file in duplicate_list)
                {
                    FileInfo fi = new FileInfo(file);
                    duplicate_ordered.Add(fi.LastAccessTime, file);
                }

                List<string> files_in_list = null;

                if (AutoOldest == true && AutoNewest == false)
                {
                    files_in_list = duplicate_ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep OLDEST file: ");

                    if ( Verbose == true )
                    {
                        Console.WriteLine(files_in_list[0]);
                    }
                    else if ( Verbose == false )
                    {
                        Console.WriteLine(Path.GetFileName(files_in_list[0]));
                    }

                    files_in_list.RemoveAt(0);
                }
                else if (AutoOldest == false && AutoNewest == true)
                {
                    files_in_list = duplicate_ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep NEWEST file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(files_in_list[files_in_list.Count - 1]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(files_in_list[files_in_list.Count -1 ]));
                    }

                }
                else if (AutoOldest == false && AutoNewest == false)
                {
                    files_in_list = SelectFileToKeep(DuplicateLists.Keys[DuplicateLists.IndexOfValue(duplicate_list)], duplicate_ordered);
                }

                if (files_in_list.Count != 0)
                {
                    RemoveLists.Add(DuplicateLists.Keys[DuplicateLists.IndexOfValue(duplicate_list)], files_in_list);
                }
            }
            Console.WriteLine();

            foreach (List<string> removes in RemoveLists.Values)
            {
                Console.WriteLine("Removing duplicates of hash: " + RemoveLists.Keys[RemoveLists.IndexOfValue(removes)]);

                foreach (string file in removes)
                {
                    RemoveFile(file);
                }
            }

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

        static void AnalyzeFilelist(out SortedList<string,List<string>> duplicate_lists)
        {
            IEnumerable<string> duplicate_hashes =
                Files.GroupBy(f => f.Value).Where(v => v.Count() > 1).Select(h => h.Key);

            duplicate_lists = new SortedList<string, List<string>>();
            
            foreach (string hash in duplicate_hashes)
            {
                IEnumerable<string> duplicates = Files.Where(d => d.Value == hash).Select(s => s.Key);
                duplicate_lists.Add(hash, duplicates.ToList());
            }
        }

        static List<string> SelectFileToKeep(string hash, SortedList<DateTime,string> duplicate_ordered)
        {
            bool good_selection = false;
            int choice = 0;

            while (!good_selection)
            {
                Console.WriteLine("=================================");
                Console.WriteLine("The following files are duplicates of each other: ");

                if (Verbose == true)
                {
                    Console.WriteLine("Hash: " + hash);
                }

                foreach (KeyValuePair<DateTime, string> duplicate in duplicate_ordered)
                {
                    Console.Write((duplicate_ordered.IndexOfValue(duplicate.Value) + 1) + ". ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(duplicate.Value);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(duplicate.Value));
                    }

                    Console.Write("Last modified: " +
                        duplicate.Key.ToString(System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat));

                    if (duplicate_ordered.IndexOfValue(duplicate.Value) == 0)
                    {
                        Console.Write(" (oldest)");
                    }

                    if (duplicate_ordered.IndexOfValue(duplicate.Value) == duplicate_ordered.Count - 1)
                    {
                        Console.Write(" (newest)");
                    }

                    Console.WriteLine();
                }
                Console.WriteLine((duplicate_ordered.Count + 1) + ". Take no action");

                try
                {
                    Console.Write("Select the file you want TO KEEP. (The rest will be deleted.): ");
                    choice = Convert.ToInt32(Console.ReadLine());

                    good_selection = true;

                    if (choice < 1 || choice > duplicate_ordered.Count + 1)
                    {
                        throw new Exception("You entered an invalid option. Please choose an option printed on the menu.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to parse choice. An exception happened: " + ex.Message);
                    good_selection = false;
                }
            }

            if (choice != duplicate_ordered.Count + 1 && choice != 0)
            {
                Console.Write("Scheduled to keep file: ");

                if (Verbose == true)
                {
                    Console.WriteLine(duplicate_ordered.Values[choice - 1]);
                }
                else if (Verbose == false)
                {
                    Console.WriteLine(Path.GetFileName(duplicate_ordered.Values[choice - 1]));
                }

                duplicate_ordered.RemoveAt(choice - 1);
            }
            else if (choice == duplicate_ordered.Count + 1 || choice == 0)
            {
                Console.WriteLine("All files will be kept.");
                duplicate_ordered.Clear();
            }

            return duplicate_ordered.Values.ToList();
        }

        static string CalculateHash(ref System.Security.Cryptography.MD5CryptoServiceProvider mcsp, string file)
        {
            string md5b64;

            if (Verbose == true)
            {
                Console.Write("Reading file " + Path.GetFileName(file) + "...");
            }

            using (FileStream stream = File.OpenRead(file))
            {
                byte[] filebytes = new byte[stream.Length + 1];
                stream.Read(filebytes, 0, Convert.ToInt32(stream.Length));
                byte[] md5bytes = mcsp.ComputeHash(filebytes);
                md5b64 = Convert.ToBase64String(md5bytes);
            }

            if (Verbose == true)
            {
                Console.WriteLine(" Read. Hash: " + md5b64 + ".");
            }

            return md5b64;
        }

        static void RemoveFile(string file)
        {
            Console.Write("File " + Path.GetFileName(file) + " was scheduled for deletion.");

            try
            {
                if (ShouldRemove == true)
                {
                    //File.Delete(file);
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
        
        static void DirSearch(string directory)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider mcsp = new System.Security.Cryptography.MD5CryptoServiceProvider();

            try
            {
                foreach (string f in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
                {
                    Files.Add(f, CalculateHash(ref mcsp, f));
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