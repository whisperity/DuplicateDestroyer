using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DuplicateDestroyer
{
    static class Program
    {
        static bool AutoOldest;
        static bool AutoNewest;
        static bool Verbose;
        static bool FileRemoveException;
        static Dictionary<string, string> Files;
        static int FileCount;

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
                Console.WriteLine("-v       Verbose mode");
                Console.WriteLine("-o       Automatically schedule the OLDEST file for keeping.");
                Console.WriteLine("-n       Automatically schedule the NEWEST file for keeping.");
                Console.WriteLine();
                Console.WriteLine("Omitting both -o and -n results in the user being queried about which file to keep.");
                Console.WriteLine("Using both -o and -n with each other is an error.");
                Console.WriteLine();

                Environment.Exit(0);
            }

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
            FileCount = CountFiles(Directory.GetCurrentDirectory());
            Console.WriteLine(Convert.ToString(FileCount) + " files found.");
            Files = new Dictionary<string, string>(FileCount);
            Console.WriteLine();

            DirSearch(Directory.GetCurrentDirectory());
            Console.WriteLine();

            SortedList<string, List<string>> DuplicateHashesList;
            GetDuplicateHashes(out DuplicateHashesList);
            Console.WriteLine();

            SortedList<string, List<string>> RemoveLists = new SortedList<string, List<string>>(DuplicateHashesList.Count);
            foreach (List<string> duplicate_hash_files in DuplicateHashesList.Values)
            {
                SortedList<DateTime, string> ordered = new SortedList<DateTime, string>(duplicate_hash_files.Count);

                foreach (string file in duplicate_hash_files)
                {
                    FileInfo fi = new FileInfo(file);
                    ordered.Add(fi.LastAccessTime, file);
                }

                List<string> files_to_remove = null;

                if (AutoOldest == true && AutoNewest == false)
                {
                    files_to_remove = ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep OLDEST file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(files_to_remove[0]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(files_to_remove[0]));
                    }

                    files_to_remove.RemoveAt(0);
                }
                else if (AutoOldest == false && AutoNewest == true)
                {
                    files_to_remove = ordered.Values.ToList();

                    Console.Write("Automatically scheduled to keep NEWEST file: ");

                    if (Verbose == true)
                    {
                        Console.WriteLine(files_to_remove[files_to_remove.Count - 1]);
                    }
                    else if (Verbose == false)
                    {
                        Console.WriteLine(Path.GetFileName(files_to_remove[files_to_remove.Count - 1]));
                    }

                    files_to_remove.RemoveAt(files_to_remove.Count - 1);
                }
                else if (AutoOldest == false && AutoNewest == false)
                {
                    files_to_remove = SelectFileToKeep(
                        DuplicateHashesList.Keys[DuplicateHashesList.IndexOfValue(duplicate_hash_files)], ordered);
                }

                if (files_to_remove.Count != 0)
                {
                    RemoveLists.Add(DuplicateHashesList.Keys[DuplicateHashesList.IndexOfValue(duplicate_hash_files)], files_to_remove);
                }
            }
            Console.WriteLine();

            foreach (List<string> removes in RemoveLists.Values)
            {
                if (Verbose == true)
                {
                    Console.WriteLine("Removing duplicates of hash: " + RemoveLists.Keys[RemoveLists.IndexOfValue(removes)]);
                }

                foreach (string file in removes)
                {
                    RemoveFile(file);
                }

                if (Verbose == true)
                {
                    Console.WriteLine();
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

        static void GetDuplicateHashes(out SortedList<string, List<string>> duplicateLists)
        {
            IEnumerable<string> duplicate_hashes =
                Files.GroupBy(f => f.Value).Where(v => v.Count() > 1).Select(h => h.Key);

            duplicateLists = new SortedList<string, List<string>>();

            foreach (string hash in duplicate_hashes)
            {
                IEnumerable<string> duplicates = Files.Where(d => d.Value == hash).Select(s => s.Key);
                duplicateLists.Add(hash, duplicates.ToList());
            }
        }

        static List<string> SelectFileToKeep(string hash, SortedList<DateTime, string> fileList)
        {
            bool selection_success = false;
            uint choice = 0;

            while (!selection_success)
            {
                Console.WriteLine("=================================");
                Console.WriteLine("The following files are duplicates of each other: ");

                if (Verbose == true)
                {
                    Console.WriteLine("(Hash: " + hash + ")");
                }

                foreach (KeyValuePair<DateTime, string> duplicate in fileList)
                {
                    Console.Write((fileList.IndexOfValue(duplicate.Value) + 1) + ". ");

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

                    if (fileList.IndexOfValue(duplicate.Value) == 0)
                    {
                        Console.Write(" (oldest)");
                    }

                    if (fileList.IndexOfValue(duplicate.Value) == fileList.Count - 1)
                    {
                        Console.Write(" (newest)");
                    }

                    Console.WriteLine();
                }
                Console.WriteLine((fileList.Count + 1) + ". Take no action");

                try
                {
                    Console.Write("Select the file you want TO KEEP. (The rest will be deleted.): ");
                    choice = Convert.ToUInt32(Console.ReadLine());

                    selection_success = true;

                    if (choice < 1 || choice > fileList.Count + 1)
                    {
                        throw new Exception("You entered an invalid option. Please choose an option printed on the menu.");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to parse choice. An exception happened: " + ex.Message);
                    selection_success = false;
                }
            }

            if (choice != fileList.Count + 1 && choice != 0)
            {
                Console.Write("Scheduled to keep file: ");

                if (Verbose == true)
                {
                    Console.WriteLine(fileList.Values[(int)choice - 1]);
                }
                else if (Verbose == false)
                {
                    Console.WriteLine(Path.GetFileName(fileList.Values[(int)choice - 1]));
                }

                fileList.RemoveAt((int)choice - 1);
            }
            else if (choice == fileList.Count + 1 || choice == 0)
            {
                Console.WriteLine("All files will be kept.");
                fileList.Clear();
            }

            return fileList.Values.ToList();
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
            Console.Write("File " + Path.GetFileName(file) + " ...");

            try
            {
                File.Delete(file);
                Console.WriteLine(" Deleted.");
            }
            catch (Exception ex)
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