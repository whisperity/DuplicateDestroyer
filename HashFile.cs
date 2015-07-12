using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DuplicateDestroyer
{
    struct SizeHashEntry
    {
        public List<HashPointers> Pointers;
    }

    struct HashPointers
    {
        public string Hash; // 32, md5 is used
        public List<long> FileEntries;
    }

    class HashFile
    {
        internal FileStream Stream;

        public HashFile(FileStream stream)
        {
            this.Stream = stream;
        }

        public long WriteRecord(SizeHashEntry entry)
        {
            this.Stream.Seek(0, SeekOrigin.End);
            long writePosition = this.Stream.Position;

            // The layout in the file is as follows:
            // Number of HashPointers and then the block of hash pointers
            // Each block begins with a Hash string, then a number of file entry pointers, then those pointers itself

            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                bw.Write(entry.Pointers.Count); // int 4
                foreach (HashPointers hp in entry.Pointers)
                {
                    bw.Write(Encoding.UTF8.GetBytes(hp.Hash)); // 32
                    bw.Write(hp.FileEntries.Count); // int 4
                    foreach (long fileEntry in hp.FileEntries)
                        bw.Write(fileEntry); // 8
                }
            }

            return writePosition;
        }
    }
}
