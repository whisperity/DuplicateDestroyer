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
        public const int RecordSize = 8 + 8;

        public ulong Size; // 8
        public ulong Count; // 8
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
                    this.LastPosition = this.Stream.Position;
                }
            }
        }

        internal IEnumerable<SizeEntry> GetRecords()
        {
            this.Stream.Seek(0, SeekOrigin.Begin);

            using (BinaryReader br = new BinaryReader(this.Stream, Encoding.UTF8, true))
            {
                long lastReadPosition = 0;
                while (lastReadPosition < this.Stream.Length)
                {
                    SizeEntry se = new SizeEntry();
                    se.Size = br.ReadUInt64(); // 8
                    se.Count = br.ReadUInt64(); // 8

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

                MoveEndPart(position, SizeEntry.RecordSize);

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
                bw.Flush();
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
                MoveEndPart(position + SizeEntry.RecordSize, -SizeEntry.RecordSize);

                this.LastPosition -= SizeEntry.RecordSize;
            }
            
            // If the record is not found, there is nothing to delete.
        }
        
        private const int MaxBufferSize = 2 << 11; // 2^12 = 4096, 4 KiB

        public void MoveEndPart(long position, long difference)
        {
            // Move all the bytes from position to the end of the stream to a new position.
            // (In either direction.)
            if (position < 0)
                throw new ArgumentOutOfRangeException("The initial position from where the content should be moved can't be negative.");
            else if (position > this.Stream.Length)
                throw new ArgumentOutOfRangeException("The initial position from where the content should be moved must be inside the stream.");

            if (difference == 0 || position == this.Stream.Length)
                return; // Noop, nothing to move.
            
            if (position + difference < 0)
                throw new ArgumentOutOfRangeException("Requested to move bytes before the beginning of the stream.");

            // First, we calculate how many bytes are there to be moved.
            long fullByteCount = this.Stream.Length - position;

            // This blob is to be chunked up based on MaxBufferSize.
            // For every move operation, a such buffer will be read and written out.
            this.Stream.Seek(position, SeekOrigin.Begin);
            long currentPosition = position;
            long newPosition = position + difference; // Where the moved bytes will begin after the move

            if (fullByteCount == 0)
                return; // Noop, nothing to move.

            // Calculate a buffer size to use
            int bufferSize;
            if (fullByteCount > MaxBufferSize)
                bufferSize = MaxBufferSize;
            else
                //bufferSize = (int)Math.Pow(2, Math.Ceiling(Math.Log(fullByteCount, 2)));
                bufferSize = (int)fullByteCount;
            byte[] buffer = new byte[bufferSize];

            long byteCount = 0; // The count of "done" bytes we already moved
            long readPosition = -1, writePosition = -1; // Two pointers where the next read and write operation will work.

            if (difference > 0)
            {
                // If we are moving FORWARD, the first chunk to be read is the LAST in the file.
                // We start from the right.
                readPosition = this.Stream.Length - bufferSize;
                writePosition = readPosition + difference;

                // Also, if we are moving forward, the stream has to be increased in size.
                this.Stream.SetLength(this.Stream.Length + difference);
            }
            else if (difference < 0)
            {
                // If we are moving BACKWARDS, the first chunk to be read is the FIRST
                // We start from the left.
                readPosition = position;
                writePosition = readPosition + difference; // (well, actually a - here :) )
            }

            int bytesToRead = 0;
            while (byteCount < fullByteCount)
            {
                buffer = new byte[bufferSize]; // TODO: this isn't needed, just debug cleanup.
                // If the number of remaining bytes would be smaller than the buffer size, read a partial buffer.
                if (fullByteCount - byteCount < bufferSize)
                    bytesToRead = Convert.ToInt32(fullByteCount - byteCount);
                else
                    bytesToRead = bufferSize;

                // Read the chunk.
                this.Stream.Seek(readPosition, SeekOrigin.Begin);
                this.Stream.Read(buffer, 0, bytesToRead);

                // And write it.
                this.Stream.Seek(writePosition, SeekOrigin.Begin);
                this.Stream.Write(buffer, 0, bytesToRead);
                this.Stream.Flush(true);

                // Align the two intermediate pointers to the new locations for the next operation.
                // (The read and write positions should always be having a distance of 'difference' between each other.)
                if (difference > 0)
                {
                    // If we are moving the bytes FORWARD, the read head moves BACKWARDS, because we started from the right.

                    // Read and write positions could underflow this way.
                    // If the last remaining chunk is smaller than the buffer and would begin before the initial start position...
                    // we correct it.
                    if (readPosition - bytesToRead < position)
                    {
                        readPosition = position;
                        writePosition = position + difference;
                    }
                    else
                    {
                        readPosition -= bytesToRead;
                        writePosition -= bytesToRead;
                    }
                }
                else if (difference < 0)
                {
                    // If we are moving the bytes BACKWARD, the read and write moves FORWARD, because we started form the left.
                    readPosition += bytesToRead;
                    writePosition += bytesToRead;
                }

                byteCount += bytesToRead; // Mark the currently done bytes... 'done'
            }

            this.Stream.Flush(true);
            if (difference < 0)
                // If the move operation was to shrink, we eliminate the overhead at the end of the file.
                this.Stream.SetLength(this.Stream.Length + difference); // (still a - :) )
        }
    }
}