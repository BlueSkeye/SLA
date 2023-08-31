using Sla.CORE;

namespace Sla.DECCORE
{
    /// \brief A simple raw binary loadimage
    ///
    /// This is probably the simplest loadimage.  Bytes from the image are read directly from a file stream.
    /// The address associated with each byte is determined by a single value, the vma, which is the address
    /// of the first byte in the file.  No symbols or sections are supported
    internal class RawLoadImage : LoadImage
    {
        /// Address of first byte in the file
        private ulong vma;
        /// Main file stream for image
        private FileStream? thefile;
        /// Total number of bytes in the loadimage/file
        private ulong filesize;
        /// Address space that the file bytes are mapped to
        private AddrSpace spaceid;

        /// RawLoadImage constructor
        public RawLoadImage(string f)
        {
            vma = 0;
            thefile = null;
            spaceid = (AddrSpace)null;
            filesize = 0;
        }

        /// Attach the raw image to a particular space
        public void attachToSpace(AddrSpace id)
        {
            spaceid = id;
        }

        /// Open the raw file for reading
        /// The file is opened and its size immediately recovered.
        public void open()
        {
            if (thefile != null)
                throw new LowlevelError("loadimage is already open");
            try { thefile = File.OpenRead(filename); }
            catch {
                throw new LowlevelError($"Unable to open raw image file: {filename}");
            }
            thefile.Seek(0, SeekOrigin.End);
            filesize = (ulong)thefile.Length;
        }

        /// RawLoadImage destructor
        ~RawLoadImage()
        {
            if (thefile != null) {
                thefile.Close();
                // delete thefile;
            }
        }

        public override void loadFill(byte[] ptr, int size, Address addr)
        {
            ulong curaddr = addr.getOffset();
            ulong offset = 0;
            ulong readsize;

            // Get relative offset of first byte
            curaddr -= vma;
            while (size > 0) {
                if (curaddr >= filesize) {
                    if (offset == 0)
                        // Initial address not within file
                        break;
                    // Fill out the rest of the buffer with 0
                    Array.Fill(ptr, (byte)0, (int)offset, size);
                    return;
                }
                readsize = (ulong)size;
                if (curaddr + readsize > filesize)
                    // Adjust to biggest possible read
                    readsize = filesize - curaddr;
                thefile.Seek((long)curaddr, SeekOrigin.Begin);
                thefile.Read(ptr, (int)offset, (int)readsize);
                offset += readsize;
                size -= (int)readsize;
                curaddr += readsize;
            }
            if (size > 0) {
                TextWriter errmsg = new StringWriter();
                errmsg.Write($"Unable to load {size} bytes at {addr.getShortcut()}");
                addr.printRaw(errmsg);
                throw new DataUnavailError(errmsg.ToString());
            }
        }

        public override string getArchType() => "unknown";

        public override void adjustVma(ulong adjust)
        {
            adjust = AddrSpace.addressToByte(adjust, spaceid.getWordSize());
            vma += adjust;
        }
    }
}
