/* ###
 * IP: GHIDRA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
//#include "pcoderaw.hh"
//#include "translate.hh"

using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;

namespace Sla.CORE
{
    /// \brief A low-level representation of a single pcode operation
    /// This is just the minimum amount of data to represent a pcode operation
    /// An opcode, sequence number, optional output varnode
    /// and input varnodes
    public class PcodeOpRaw
    {
        /// The opcode for this operation
        private OpBehavior behave;
        ///Identifying address and index of this operation
        private SeqNum seq;
        /// Output varnode triple
        private VarnodeData @out;
        /// Raw varnode inputs to this op
        private List<VarnodeData> @in;

        /// Set the opcode for this op
        /// The core behavior for this operation is controlled by an OpBehavior object
        /// which knows how output is determined given inputs. This routine sets that object
        /// \param be is the behavior object
        public void setBehavior(OpBehavior be)
        {
            behave = be;
        }

        /// Retrieve the behavior for this op
        /// Get the underlying behavior object for this pcode operation.  From this
        /// object you can determine how the object evaluates inputs to get the output
        /// \return the behavior object
        public OpBehavior getBehavior() => behave;

        /// Get the opcode for this op
        /// The possible types of pcode operations are enumerated by OpCode
        /// This routine retrieves the enumeration value for this particular op
        /// \return the opcode value
        public OpCode getOpcode() => behave.getOpcode();

        /// Set the sequence number
        /// Every pcode operation has a \b sequence \b number
        /// which associates the operation with the address of the machine instruction
        /// being translated and an order number which provides an index for this
        /// particular operation within the entire translation of the machine instruction
        /// \param a is the instruction address
        /// \param b is the order number
        public void setSeqNum(ref Address a, uint b)
        {
            seq = new SeqNum(a, b);
        }

        /// Retrieve the sequence number
        /// Every pcode operation has a \b sequence \b number which associates
        /// the operation with the address of the machine instruction being translated
        /// and an index number for this operation within the translation.
        /// \return a reference to the sequence number
        public SeqNum getSeqNum() => seq;

        /// Get address of this operation
        /// This is a convenience function to get the address of the machine instruction
        /// (of which this pcode op is a translation)
        /// \return the machine instruction address
        public Address getAddr() => seq.getAddr();

        /// Set the output varnode for this op
        /// Most pcode operations output to a varnode.  This routine sets what that varnode is.
        /// \param o is the varnode to set as output
        public void setOutput(VarnodeData o)
        {
            @out = o;
        }

        /// Retrieve the output varnode for this op
        /// Most pcode operations have an output varnode. This routine retrieves that varnode.
        /// \return the output varnode or \b null if there is no output
        public VarnodeData getOutput() => @out;

        /// Add an additional input varnode to this op
        /// A PcodeOpRaw is initially created with no input varnodes.  Inputs are added with this method.
        /// Varnodes are added in order, so the first addInput call creates input 0, for example.
        /// \param i is the varnode to be added as input
        public void addInput(VarnodeData i)
        {
            @in.Add(i);
        }

        /// Remove all input varnodes to this op
        /// If the inputs to a pcode operation need to be changed, this routine clears the existing
        /// inputs so new ones can be added.
        public void clearInputs()
        {
            @in.Clear();
        }

        /// Get the number of input varnodes to this op
        /// \return the number of inputs
        public int numInput() => @in.Count;

        /// Get the i-th input varnode for this op
        /// Input varnodes are indexed starting at 0.  This retrieves the input varnode by index.
        /// The index \e must be in range, or unpredicatable behavior will result. Use the numInput method
        /// to get the number of inputs.
        /// \param i is the index of the desired input
        /// \return the desired input varnode
        public VarnodeData getInput(int i) => @in[i];

        /// \brief Decode the raw OpCode and input/output Varnode data for a PcodeOp
        /// This assumes the \<op> element is already open.
        /// Decode info suitable for call to PcodeEmit::dump.  The output pointer is changed to null if there
        /// is no output for this op, otherwise the existing pointer is used to store the output.
        /// \param decoder is the stream decoder
        /// \param isize is the (preparsed) number of input parameters for the p-code op
        /// \param invar is an array of storage for the input Varnodes
        /// \param outvar is a (handle) to the storage for the output Varnode
        /// \return the p-code op OpCode
        public static OpCode decode(Sla.CORE.Decoder decoder, int isize, VarnodeData[] invar,
            out VarnodeData? outvar)
        {
            OpCode opcode = (OpCode)decoder.readSignedInteger(AttributeId.ATTRIB_CODE);
            uint subId = decoder.peekElement();
            if (subId == ElementId.ELEM_VOID) {
                decoder.openElement();
                decoder.closeElement(subId);
                outvar = null;
            }
            else {
                outvar = VarnodeData.decode(decoder);
            }
            for (int i = 0; i < isize; ++i) {
                subId = decoder.peekElement();
                if (subId == ElementId.ELEM_SPACEID) {
                    decoder.openElement();
                    invar[i].space = decoder.getAddrSpaceManager().getConstantSpace();
                    // WARNING : Was stored in the offset field.
                    invar[i].subSpace = decoder.readSpace(AttributeId.ATTRIB_NAME);
                    invar[i].size = (uint)IntPtr.Size;
                    decoder.closeElement(subId);
                }
                else {
                    invar[i] = VarnodeData.decode(decoder);
                }
            }
            return opcode;
        }
    }
}
