using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An interface into a particular binary executable image
    ///
    /// This class provides the abstraction needed by the decompiler
    /// for the numerous load file formats used to encode binary
    /// executables.  The data encoding the machine instructions
    /// for the executable can be accessed via the addresses where
    /// that data would be loaded into RAM.
    /// Properties other than the main data and instructions of the
    /// binary are not supposed to repeatedly queried through this
    /// interface. This information is intended to be read from
    /// this class exactly once, during initialization, and used to
    /// populate the main decompiler database. This class currently
    /// has only rudimentary support for accessing such properties.
    internal abstract class LoadImage
    {
        /// Name of the loadimage
        protected string filename;

        /// LoadImage constructor
        /// For the base class there is no relevant initialization except
        /// the name of the image.
        /// \param f is the name of the image
        public LoadImage(string f)
        {
            filename = f;
        }

        /// The destructor for the load image object.
        ~LoadImage()
        {
        }

        /// Get the name of the LoadImage
        /// The loadimage is usually associated with a file. This routine
        /// retrieves the name as a string.
        /// \return the name of the image
        public string getFileName() => filename;

        /// Get data from the LoadImage
        /// \fn void LoadImage::loadFill(uint1 *ptr,int4 size,const Address &addr)
        /// This is the \e core routine of a LoadImage.  Given a particular
        /// address range, this routine retrieves the exact byte values
        /// that are stored at that address when the executable is loaded
        /// into RAM.  The caller must supply a pre-allocated array
        /// of bytes where the returned bytes should be stored.  If the
        /// requested address range does not exist in the image, or
        /// otherwise can't be retrieved, this method throws an
        /// DataUnavailError exception.
        /// \param ptr points to where the resulting bytes will be stored
        /// \param size is the number of bytes to retrieve from the image
        /// \param addr is the starting address of the bytes to retrieve
        public abstract void loadFill(uint1 ptr, int4 size, Address addr);

        /// Prepare to read symbols
        /// This routine should read in and parse any symbol information
        /// that the load image contains about executable.  Once this
        /// method is called, individual symbol records are read out
        /// using the getNextSymbol() method.
        public virtual void openSymbols()
        {
        }

        /// Stop reading symbols
        /// Once all the symbol information has been read out from the
        /// load image via the openSymbols() and getNextSymbol() calls,
        /// the application should call this method to free up resources
        /// used in parsing the symbol information.
        public virtual void closeSymbols()
        {
        }

        /// Get the next symbol record
        /// This method is used to read out an individual symbol record,
        /// LoadImageFunc, from the load image.  Right now, the only
        /// information that can be read out are function starts and the
        /// associated function name.  This method can be called repeatedly
        /// to iterate through all the symbols, until it returns \b false.
        /// This indicates the end of the symbols.
        /// \param record is a reference to the symbol record to be filled in
        /// \return \b true if there are more records to read
        public virtual bool getNextSymbol(LoadImageFunc record)
        {
            return false;
        }

        /// Prepare to read section info
        /// This method initializes iteration over all the sections of
        /// bytes that are mapped by the load image.  Once this is called,
        /// information on individual sections should be read out with
        /// the getNextSection() method.
        public virtual void openSectionInfo()
        {
        }

        /// Stop reading section info
        /// Once all the section information is read from the load image
        /// using the getNextSection() method, this method should be
        /// called to free up any resources used in parsing the section info.
        public virtual void closeSectionInfo()
        {
        }

        /// Get info on the next section
        /// This method is used to read out a record that describes a
        /// single section of bytes mapped by the load image. This
        /// method can be called repeatedly until it returns \b false,
        /// to get info on additional sections.
        /// \param record is a reference to the info record to be filled in
        /// \return \b true if there are more records to read
        public virtual bool getNextSection(LoadImageSection sec)
        {
            return false;
        }

        /// Return list of \e readonly address ranges
        /// This method should read out information about \e all
        /// address ranges within the load image that are known to be
        /// \b readonly.  This method is intended to be called only
        /// once, so all information should be written to the passed
        /// RangeList object.
        /// \param list is where readonly info will get put
        public virtual void getReadonly(RangeList list)
        {
        }

        /// Get a string indicating the architecture type
        /// \fn string LoadImage::getArchType(void) const
        /// The load image class is intended to be a generic front-end
        /// to the large variety of load formats in use.  This method
        /// should return a string that identifies the particular
        /// architecture this particular image is intended to run on.
        /// It is currently the responsibility of any derived LoadImage
        /// class to establish a format for this string, but it should
        /// generally contain some indication of the operating system
        /// and the processor.
        /// \return the identifier string
        public abstract string getArchType();

        /// Adjust load addresses with a global offset
        /// \fn void LoadImage::adjustVma(long adjust)
        /// Most load image formats automatically encode information
        /// about the true loading address(es) for the data in the image.
        /// But if this is missing or incorrect, this routine can be
        /// used to make a global adjustment to the load address. Only
        /// one adjustment is made across \e all addresses in the image.
        /// The offset passed to this method is added to the stored
        /// or default value for any address queried in the image.
        /// This is most often used in a \e raw binary file format.  In
        /// this case, the entire executable file is intended to be
        /// read straight into RAM, as one contiguous chunk, in order to
        /// be executed.  In the absence of any other info, the first
        /// byte of the image file is loaded at offset 0. This method
        /// then would adjust the load address of the first byte.
        /// \param adjust is the offset amount to be added to default values
        public abstract void adjustVma(long adjust);

        /// Load a chunk of image
        /// This is a convenience method wrapped around the core
        /// loadFill() routine.  It automatically allocates an array
        /// of the desired size, and then fills it with load image data.
        /// If the array cannot be allocated, an exception is thrown.
        /// The caller assumes the responsibility of freeing the
        /// array after it has been used.
        /// \param size is the number of bytes to read from the image
        /// \param addr is the address of the first byte being read
        /// \return a pointer to the desired bytes
        public virtual uint1[] load(int4 size, Address addr)
        {
            uint1[] buf = new uint1[size];
            if (buf == (uint1*)0)
                throw new LowlevelError("Out of memory");
            loadFill(buf, size, addr);
            return buf;
        }
    }
}
