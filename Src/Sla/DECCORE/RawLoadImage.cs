using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        private ifstream thefile;
        /// Total number of bytes in the loadimage/file
        private ulong filesize;
        /// Address space that the file bytes are mapped to
        private AddrSpace spaceid;

        /// RawLoadImage constructor
        public RawLoadImage(string f)
        {
            vma = 0;
            thefile = (ifstream*)0;
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
            if (thefile != (ifstream*)0) throw new LowlevelError("loadimage is already open");
            thefile = new ifstream(filename.c_str());
            if (!(*thefile))
            {
                string errmsg = "Unable to open raw image file: " + filename;
                throw new LowlevelError(errmsg);
            }
            thefile.seekg(0, ios::end);
            filesize = thefile.tellg();
        }

        /// RawLoadImage destructor
        ~RawLoadImage()
        {
            if (thefile != (ifstream*)0)
            {
                thefile.close();
                delete thefile;
            }
        }

        public override void loadFill(byte ptr, int size, Address addr)
        {
            ulong curaddr = addr.getOffset();
            ulong offset = 0;
            ulong readsize;

            curaddr -= vma;     // Get relative offset of first byte
            while (size > 0)
            {
                if (curaddr >= filesize)
                {
                    if (offset == 0)        // Initial address not within file
                        break;
                    memset(ptr + offset, 0, size); // Fill out the rest of the buffer with 0
                    return;
                }
                readsize = size;
                if (curaddr + readsize > filesize) // Adjust to biggest possible read
                    readsize = filesize - curaddr;
                thefile.seekg(curaddr);
                thefile.read((char*)(ptr + offset), readsize);
                offset += readsize;
                size -= readsize;
                curaddr += readsize;
            }
            if (size > 0)
            {
                ostringstream errmsg;
                errmsg << "Unable to load " << dec << size << " bytes at " << addr.getShortcut();
                addr.printRaw(errmsg);
                throw new DataUnavailError(errmsg.str());
            }
        }

        public override string getArchType() => "unknown";

        public override void adjustVma(long adjust)
        {
            adjust = AddrSpace::addressToByte(adjust, spaceid.getWordSize());
            vma += adjust;
        }
    }
}
