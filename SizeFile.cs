using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DuplicateDestroyer
{
    struct SizeEntry
    {
        public const int RecordSize = 8 + 8 + 8 + 8 + 8;

        public ulong Size; // 8
        public ulong Count; // 8
        public long FirstPath; // 8
        public long LastPath; // 8
        public long HashEntry; // 8
    }

    class SizeFile
    {
        internal FileStream Stream;
        private long FirstPosition;
        private long LastPosition;

        internal long RecordCount { get { return LastPosition / SizeEntry.RecordSize; } }

        public SizeFile(FileStream stream)
        {
            this.Stream = stream;
            this.FirstPosition = 0;
            this.LastPosition = 0;

            this.Stream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                while (this.LastPosition < this.Stream.Length)
                {
                    br.ReadUInt64(); // Size (8)
                    br.ReadUInt64(); // Count (8)
                    br.ReadInt64(); // First path (8)
                    br.ReadInt64(); // Last path (8)
                    br.ReadInt64(); // Hash entry (8)
                    this.LastPosition = this.Stream.Position;
                }
            }
        }

        public IEnumerable<SizeEntry> GetRecords()
        {
            this.Stream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                long lastReadPosition = 0;
                while (lastReadPosition < this.Stream.Length)
                {
                    this.Stream.Seek(lastReadPosition, SeekOrigin.Begin);
                    SizeEntry se = new SizeEntry();
                    se.Size = br.ReadUInt64(); // 8
                    se.Count = br.ReadUInt64(); // 8
                    se.FirstPath = br.ReadInt64(); // 8
                    se.LastPath = br.ReadInt64(); // 8
                    se.HashEntry = br.ReadInt64();  // 8

                    lastReadPosition = this.Stream.Position;
                    yield return se;
                }
            }
        }

        public bool GetRecord(ulong size, out SizeEntry record, out long position)
        {
            record = new SizeEntry();
            position = 0;

            if (this.RecordCount == 0)
                return false; // If there is nothing, there is nothing to find.
            
            // Because the data is ordered DESCENDING, we can use binary search which is a lot more effective.

            long low = 0;
            long high = this.RecordCount - 1;

            bool found = false;
            while (low <= high && !found)
            {
                // Try the record at the middle
                long mid = (low + high) / 2;
                position = mid * SizeEntry.RecordSize;
                record = GetRecordAt(position);

                if (size < record.Size)
                    // The searched size is smaller than the middle's size.
                    // The record --if exists-- is in the RIGHT half of the interval
                    low = mid + 1;
                else if (size > record.Size)
                    // If it's higher, it should be in the LEFT half because descending order.
                    high = mid - 1;
                else
                    found = true;
            }

            // If the record is not found, position should contain the place where it should be inserted.
            if (!found)
                position = low * SizeEntry.RecordSize;

            return found;
        }

        public SizeEntry GetRecordByIndex(long index)
        {
            if (index > RecordCount || index < 0)
                throw new ArgumentOutOfRangeException("Can't match index with a record: index is negative or too big.");

            return GetRecordAt(index * SizeEntry.RecordSize);
        }

        private SizeEntry GetRecordAt(long position)
        {
            if (position > this.Stream.Length)
                throw new ArgumentOutOfRangeException("Position out of stream bounds.");
            else if (position % SizeEntry.RecordSize != 0)
                throw new ArgumentException("Invalid position pointing inside record.");

            SizeEntry rec = new SizeEntry();
            using (BinaryReader br = new BinaryReader(this.Stream, System.Text.Encoding.UTF8, true))
            {
                this.Stream.Seek(position, SeekOrigin.Begin);
                rec.Size = br.ReadUInt64(); // 8
                rec.Count = br.ReadUInt64(); // 8
                rec.FirstPath = br.ReadInt64(); // 8
                rec.LastPath = br.ReadInt64(); // 8
                rec.HashEntry = br.ReadInt64(); // 8
            }

            return rec;
        }

        public void WriteRecord(SizeEntry rec)
        {
            // Check if the given record already exists
            SizeEntry already = new SizeEntry();
            long position = 0;

            if (!GetRecord(rec.Size, out already, out position))
            {
                // If the record is not found, we know the place where the record SHOULD be written to
                // to keep order. (GetRecord gives up this value as position.)
                // So at first we move every record to the right by one record size.
                Program.MoveEndPart(this.Stream, position, SizeEntry.RecordSize);

                // Modify the last position because we added a new record, so the stream contains one record more
                this.LastPosition += SizeEntry.RecordSize;
            }

            // After, we can write the record safely.
            // (If it was already found, it is an overwrite operation.)
            WriteRecordAt(rec, position);
        }

        internal void WriteRecordAt(SizeEntry rec, long position)
        {
            if (position > (this.Stream.Length + 1)) // Length + 1 can be written: it is when the stream extends.
                throw new ArgumentOutOfRangeException("Position out of stream bounds.");
            else if (position % SizeEntry.RecordSize != 0)
                throw new ArgumentException("Invalid position pointing inside record.");

            using (BinaryWriter bw = new BinaryWriter(this.Stream, Encoding.UTF8, true))
            {
                this.Stream.Seek(position, SeekOrigin.Begin);
                bw.Write(rec.Size); // 8
                bw.Write(rec.Count); // 8
                bw.Write(rec.FirstPath); // 8
                bw.Write(rec.LastPath); // 8
                bw.Write(rec.HashEntry); // 8
                this.Stream.Flush();
            }
        }

        public void DeleteRecord(ulong size)
        {
            SizeEntry already = new SizeEntry();
            long position = 0;

            if (GetRecord(size, out already, out position))
            {
                // To delete the record we simply overwrite it with all the records coming thereafter.
                // So the next record moves a whole record to the left and everything thereafter.
                Program.MoveEndPart(this.Stream, position + SizeEntry.RecordSize, -SizeEntry.RecordSize);

                this.LastPosition -= SizeEntry.RecordSize;
            }
            
            // If the record is not found, there is nothing to delete.
        }
    }
}