using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DuplicateDestroyer
{
    struct SizeHashEntry
    {
        public bool Decided;
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
            // Bool indicating whether the hash entry is decided, number of HashPointers and then the block of hash pointers
            // Each block begins with a Hash string, then a number of file entry pointers, then those pointers itself
            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                bw.Write(entry.Decided); // bool 1
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

        public long GetNextRecord(out SizeHashEntry record)
        {
            record = new SizeHashEntry();
            long beginPosition = this.Stream.Position;

            // Indicate that there are no next record
            if (this.Stream.Position >= this.Stream.Length)
                return -1;

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                record.Decided = br.ReadBoolean(); // 1
                int ptrCount = br.ReadInt32(); // 4
                record.Pointers = new List<HashPointers>(ptrCount);
                for (int i = 0; i < ptrCount; ++i)
                {
                    HashPointers hp = new HashPointers();
                    hp.Hash = Encoding.UTF8.GetString(br.ReadBytes(32)); // 32

                    int entryCount = br.ReadInt32(); // 4
                    hp.FileEntries = new List<long>(entryCount);
                    for (int j = 0; j < entryCount; ++j)
                        hp.FileEntries.Add(br.ReadInt64()); // 8

                    record.Pointers.Add(hp);
                }
            }

            return beginPosition;
        }
    }
}
