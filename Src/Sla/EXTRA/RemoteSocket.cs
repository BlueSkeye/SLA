using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.EXTRA
{
#if __REMOTE_SOCKET__
    /// \brief A wrapper around a UNIX domain socket
    ///
    /// The open() command attempts to connect to given socket name,
    /// which must have been previously established by an external process.
    /// The socket is bound to a C++ istream and ostream.
    internal class RemoteSocket
    {
        private int fileDescriptor;     ///< Descriptor for the socket
        private basic_filebuf<char> inbuf; ///< Input buffer associated with the socket
        private basic_filebuf<char> outbuf;    ///< Output buffer for the socket
        private TextReader inStream;      ///< The C++ input stream
        private TextWriter outStream;     ///< The C++ output stream
        private bool isOpen;            ///< Has the socket been opened

        public RemoteSocket()
        {
            fileDescriptor = 0;
            inbuf = (basic_filebuf<char>*)0;
            outbuf = (basic_filebuf<char>*)0;
            inStream = (FileStream)null;
            outStream = (ostream*)0;
            isOpen = false;
        }

        ~RemoteSocket()
        {
            close();
        }

        ///< Connect to the given socket
        public bool open(string filename)
        {
            if (isOpen) return false;
            if ((fileDescriptor = socket(AF_UNIX, SOCK_STREAM, 0)) < 0)
                throw IfaceError("Could not create socket");
            sockaddr_un addr;
            addr.sun_family = AF_UNIX;
            int len = filename.length();
            if (len >= sizeof(addr.sun_path))
                throw IfaceError("Socket name too long");
            memcpy(addr.sun_path, filename.c_str(), len);
            addr.sun_path[len] = '\0';
            len += sizeof(addr.sun_family);
            if (connect(fileDescriptor, (sockaddr*)&addr, len) < 0) {
                ::close(fileDescriptor);
                return false;
            }

            fdopen(fileDescriptor, "r");
            inbuf = new __gnu_cxx::stdio_filebuf<char>(fileDescriptor, ios::in);
            fdopen(fileDescriptor, "w");
            outbuf = new __gnu_cxx::stdio_filebuf<char>(fileDescriptor, ios::out);
            inStream = new istream(inbuf);
            outStream = new ostream(outbuf);
            isOpen = true;
            return true;
        }

        ///< Return \b true if the socket is ready to transfer data
        public bool isSocketOpen()
        {
            if (!isOpen) return false;
            if (inStream.eof()) {
                close();
                return false;
            }
            return true;
        }

        public TextReader getInputStream() => inStream;  ///< Get the input stream

        public TextWriter getOutputStream() => outStream; ///< Get the output stream

        ///< Close the streams and socket
        public void close()
        {
            if (inStream != (FileStream)null)
            {
                delete inStream;
                inStream = (FileStream)null;
            }
            if (outStream != (ostream*)0)
            {
                delete outStream;
                outStream = (ostream*)0;
            }
            if (inbuf != (basic_filebuf<char>*)0)
            {
                // Destroying the buffer should automatically close the socket
                delete inbuf;
                inbuf = (basic_filebuf<char>*)0;
            }
            if (outbuf != (basic_filebuf<char>*)0)
            {
                delete outbuf;
                outbuf = (basic_filebuf<char>*)0;
            }
            isOpen = false;
        }
    }
#endif
}
