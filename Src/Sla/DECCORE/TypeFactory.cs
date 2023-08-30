using Sla.CORE;
using Sla.DECCORE;

/// A set of data-types sorted by function
using DatatypeSet = System.Collections.Generic.HashSet<Sla.DECCORE.Datatype>; // sorted by DatatypeCompare
// A set of data-types sorted by name
using DatatypeNameSet = System.Collections.Generic.HashSet<Sla.DECCORE.Datatype>; // sorted by DatatypeNameCompare

namespace Sla.DECCORE
{
    /// \brief Container class for all Datatype objects in an Architecture
    internal class TypeFactory
    {
        /// Size of the core "int" datatype
        private int sizeOfInt;
        /// Size of the core "long" datatype
        private int sizeOfLong;
        /// Alignment of structures
        private int align;
        /// Size of an enumerated type
        private int enumsize;
        /// Default enumeration meta-type (when parsing C)
        private type_metatype enumtype;
        /// Datatypes within this factory (sorted by function)
        private DatatypeSet tree;
        /// Cross-reference by name
        private DatatypeNameSet nametree;
        /// Matrix of the most common atomic data-types
        private Datatype[][] typecache = InitializeCache();
        /// Specially cached 10-byte float type
        private Datatype? typecache10;
        /// Specially cached 16-byte float type
        private Datatype? typecache16;
        /// Same dimensions as char but acts and displays as an INT
        private Datatype? type_nochar;

        private static Datatype[][] InitializeCache()
        {
            const int firstLevelDepth = 9;
            const int secondLevelDepth = 9;
            Datatype[][] result = new Datatype[firstLevelDepth][];
            for(int index = 0; index < firstLevelDepth; index++) {
                result[index] = new Datatype[secondLevelDepth];
            }
            return result;
        }

        /// Find data-type (in this container) by function
        /// Find data-type without reference to name, using the functional comparators
        /// For this to work, the type must be built out of dependencies that are already
        /// present in \b this type factory
        /// \param ct is the data-type to match
        /// \return the matching Datatype or NULL
        private Datatype? findNoName(Datatype ct)
        {
            return (tree.Contains(ct)) ? ct : null;
        }

        /// Insert pointer into the cross-reference sets
        /// Internal method for finally inserting a new Datatype pointer
        /// \param newtype is the new pointer
        private void insert(Datatype newtype)
        {
            if (tree.Contains(newtype)) {
                StringWriter s = new StringWriter();
                s.WriteLine($"Shared type id: {newtype.getId():X}");
                s.Write("  ");
                newtype.printRaw(s);
                s.Write(" : ");
                newtype.printRaw(s);
                // delete newtype;
                throw new LowlevelError(s.ToString());
            }
            if (newtype.id != 0)
                nametree.Add(newtype);
        }

        /// Find data-type in this container or add it
        /// Use quickest method (name or id is possible) to locate the matching data-type.
        /// If its not currently in \b this container, clone the data-type and add it to the container.
        /// \param ct is the data-type to match
        /// \return the matching Datatype object in this container
        private Datatype findAdd(Datatype ct)
        {
            Datatype newtype;
            Datatype? res;

            if (ct.name.Length != 0) {
                // If there is a name
                if (ct.id == 0)     // There must be an id
                    throw new LowlevelError("Datatype must have a valid id");
                res = findByIdLocal(ct.name, ct.id); // Lookup type by it
                if (res != (Datatype)null) {
                    // If a type has this name
                    if (0 != res.compareDependency(ct)) // Check if it is the same type
                        throw new LowlevelError("Trying to alter definition of type: " + ct.name);
                    return res;
                }
            }
            else {
                res = findNoName(ct);
                if (res != (Datatype)null) return res; // Found it
            }
            newtype = ct.clone();       // Add the new type to trees
            insert(newtype);
            return newtype;
        }

        /// Write out dependency list
        /// Recursively write out all the components of a data-type in dependency order
        /// Component data-types will come before the data-type containing them in the list.
        /// \param deporder holds the ordered list of data-types to construct
        /// \param mark is a "marking" container to prevent cycles
        /// \param ct is the data-type to have written out
        private void orderRecurse(List<Datatype> deporder, DatatypeSet mark, Datatype ct)
        {
            // Make sure dependants of ct are in order, then add ct
            if (mark.Contains(ct)) return;    // Already inserted before
            if (ct.typedefImm != (Datatype)null)
                orderRecurse(deporder, mark, ct.typedefImm);
            int size = ct.numDepend();
            for (int i = 0; i < size; ++i)
                orderRecurse(deporder, mark, ct.getDepend(i));
            deporder.Add(ct);
        }

        /// Restore a \<def> element describing a typedef
        /// Scan the new id and name.  A subtag references the data-type being typedefed.
        /// Construct the new data-type based on the referenced data-type but with new name and id.
        /// \param decoder is the stream decoder
        /// \return the constructed typedef data-type
        private Datatype decodeTypedef(Decoder decoder)
        {
            ulong id = 0;
            string nm = string.Empty;
            uint format = 0;       // No forced display format by default
                                    //  ElementId elemId = decoder.openElement();
            while (true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_ID) {
                    id = decoder.readUnsignedInteger();
                }
                else if (attribId == AttributeId.ATTRIB_NAME) {
                    nm = decoder.readString();
                }
                else if (attribId == AttributeId.ATTRIB_FORMAT) {
                    format = Datatype.encodeIntegerFormat(decoder.readString());
                }
            }
            if (id == 0) {
                // Its possible the typedef is a builtin
                id = Datatype.hashName(nm);    // There must be some kind of id
            }
            Datatype defedType = decodeType(decoder);
            //  decoder.closeElement(elemId);
            if (defedType.isVariableLength()) {
                id = Datatype.hashSize(id, defedType.size);
            }
            if (defedType.getMetatype() == type_metatype.TYPE_STRUCT || defedType.getMetatype() == type_metatype.TYPE_UNION)
            {
                // Its possible that a typedef of a struct/union is recursively defined, in which case
                // an incomplete version may already be in the container
                Datatype? prev = findByIdLocal(nm, id);
                if (prev != (Datatype)null) {
                    if (defedType != prev.getTypedef())
                        throw new LowlevelError("Trying to create typedef of existing type: " + prev.name);
                    if (prev.getMetatype() == type_metatype.TYPE_STRUCT) {
                        TypeStruct prevStruct = (TypeStruct)prev;
                        TypeStruct defedStruct = (TypeStruct)defedType;
                        if (prevStruct.field.size() != defedStruct.field.size())
                            setFields(defedStruct.field, prevStruct, defedStruct.size, defedStruct.flags);
                    }
                    else {
                        TypeUnion prevUnion = (TypeUnion)prev;
                        TypeUnion defedUnion = (TypeUnion)defedType;
                        if (prevUnion.field.size() != defedUnion.field.size())
                            setFields(defedUnion.field, prevUnion, defedUnion.size, defedUnion.flags);
                    }
                    return prev;
                }
            }
            return getTypedef(defedType, nm, id, format);
        }

        /// Restore a \<type> element describing a structure
        /// If necessary create a stub object before parsing the field descriptions, to deal with recursive definitions
        /// \param decoder is the stream decoder
        /// \param forcecore is \b true if the data-type is considered core
        /// \return the newly minted structure data-type
        private Datatype decodeStruct(Decoder decoder, bool forcecore)
        {
            TypeStruct ts = new TypeStruct();
            //  ElementId elemId = decoder.openElement();
            ts.decodeBasic(decoder);
            if (forcecore)
                ts.flags |= Datatype.Properties.coretype;
            Datatype? ct = findByIdLocal(ts.name, ts.id);
            if (ct == (Datatype)null) {
                ct = findAdd(ts);   // Create stub to allow recursive definitions
            }
            else if (ct.getMetatype() != type_metatype.TYPE_STRUCT)
                throw new LowlevelError("Trying to redefine type: " + ts.name);
            ts.decodeFields(decoder, this);
            if (!ct.isIncomplete())
            {   // Structure of this name was already present
                if (0 != ct.compareDependency(ts))
                    throw new LowlevelError("Redefinition of structure: " + ts.name);
            }
            else
            {       // If structure is a placeholder stub
                if (!setFields(ts.field, (TypeStruct)ct, ts.size, ts.flags)) // Define structure now by copying fields
                    throw new LowlevelError("Bad structure definition");
            }
            //  decoder.closeElement(elemId);
            return ct;
        }

        /// Restore a \<type> element describing a union
        /// If necessary create a stub object before parsing the field descriptions, to deal with recursive definitions
        /// \param decoder is the stream decoder
        /// \param forcecore is \b true if the data-type is considered core
        /// \return the newly minted union data-type
        private Datatype decodeUnion(Decoder decoder, bool forcecore)
        {
            TypeUnion tu = new TypeUnion();
            //  ElementId elemId = decoder.openElement();
            tu.decodeBasic(decoder);
            if (forcecore)
                tu.flags |= Datatype.Properties.coretype;
            Datatype ct = findByIdLocal(tu.name, tu.id);
            if (ct == (Datatype)null) {
                ct = findAdd(tu);   // Create stub to allow recursive definitions
            }
            else if (ct.getMetatype() != type_metatype.TYPE_UNION)
                throw new LowlevelError("Trying to redefine type: " + tu.name);
            tu.decodeFields(decoder, this);
            if (!ct.isIncomplete())
            {   // Structure of this name was already present
                if (0 != ct.compareDependency(tu))
                    throw new LowlevelError("Redefinition of union: " + tu.name);
            }
            else {
                // If structure is a placeholder stub
                if (!setFields(tu.field, (TypeUnion)ct, tu.size, tu.flags)) // Define structure now by copying fields
                    throw new LowlevelError("Bad union definition");
            }
            //  decoder.closeElement(elemId);
            return ct;
        }

        /// Restore an element describing a code object
        /// If necessary create a stub object before parsing the prototype description, to deal with recursive definitions
        /// \param decoder is the stream decoder
        /// \param isConstructor is \b true if any prototype should be treated as a constructor
        /// \param isDestructor is \b true if any prototype should be treated as a destructor
        /// \param forcecore is \b true if the data-type is considered core
        /// \return the newly minted code data-type
        private Datatype decodeCode(Decoder decoder, bool isConstructor, bool isDestructor, bool forcecore)
        {
            TypeCode tc = new TypeCode();
            //  ElementId elemId = decoder.openElement();
            tc.decodeStub(decoder);
            if (tc.getMetatype() != type_metatype.TYPE_CODE) {
                throw new LowlevelError("Expecting metatype=\"code\"");
            }
            if (forcecore)
                tc.flags |= Datatype.Properties.coretype;
            Datatype ct = findByIdLocal(tc.name, tc.id);
            if (ct == (Datatype)null) {
                ct = findAdd(tc);   // Create stub to allow recursive definitions
            }
            else if (ct.getMetatype() != type_metatype.TYPE_CODE)
                throw new LowlevelError("Trying to redefine type: " + tc.name);
            tc.decodePrototype(decoder, isConstructor, isDestructor, this);
            if (!ct.isIncomplete()) {
                // Code data-type of this name was already present
                if (0 != ct.compareDependency(tc))
                    throw new LowlevelError("Redefinition of code data-type: " + tc.name);
            }
            else {
                // If there was a placeholder stub
                setPrototype(tc.proto, (TypeCode)ct, tc.flags);
            }
            //  decoder.closeElement(elemId);
            return ct;
        }

        /// Restore from a stream
        /// Restore a Datatype object from a \<type> element. (Don't use for \<typeref> elements)
        /// The new Datatype is added to \b this container.
        /// \param decoder is the stream decoder
        /// \param forcecore is true if the new type should be labeled as a core type
        /// \return the new Datatype object
        private Datatype decodeTypeNoRef(Decoder decoder, bool forcecore)
        {
            string metastring;
            Datatype ct;

            ElementId elemId = decoder.openElement();
            if (elemId == ElementId.ELEM_VOID) {
                ct = getTypeVoid(); // Automatically a coretype
                decoder.closeElement(elemId);
                return ct;
            }
            if (elemId == ElementId.ELEM_DEF) {
                ct = decodeTypedef(decoder);
                decoder.closeElement(elemId);
                return ct;
            }
            type_metatype meta = Globals.string2metatype(decoder.readString(AttributeId.ATTRIB_METATYPE));
            switch (meta) {
                case type_metatype.TYPE_PTR: {
                        TypePointer tp = new TypePointer();
                        tp.decode(decoder, this);
                        if (forcecore)
                            tp.flags |= Datatype.Properties.coretype;
                        ct = findAdd(tp);
                    }
                    break;
                case type_metatype.TYPE_PTRREL: {
                        TypePointerRel tp = new TypePointerRel();
                        tp.decode(decoder, this);
                        if (forcecore)
                            tp.flags |= Datatype.Properties.coretype;
                        ct = findAdd(tp);
                    }
                    break;
                case type_metatype.TYPE_ARRAY: {
                        TypeArray ta = new TypeArray();
                        ta.decode(decoder, this);
                        if (forcecore)
                            ta.flags |= Datatype.Properties.coretype;
                        ct = findAdd(ta);
                    }
                    break;
                case type_metatype.TYPE_STRUCT:
                    ct = decodeStruct(decoder, forcecore);
                    break;
                case type_metatype.TYPE_UNION:
                    ct = decodeUnion(decoder, forcecore);
                    break;
                case type_metatype.TYPE_SPACEBASE: {
                        TypeSpacebase tsb = new TypeSpacebase((AddrSpace)null, new Address(),glb);
                        tsb.decode(decoder, this);
                        if (forcecore)
                            tsb.flags |= Datatype.Properties.coretype;
                        ct = findAdd(tsb);
                    }
                    break;
                case type_metatype.TYPE_CODE:
                    ct = decodeCode(decoder, false, false, forcecore);
                    break;
                default:
                    while (true) {
                        AttributeId attribId = decoder.getNextAttributeId();
                        if (attribId == 0) break;
                        if (attribId == AttributeId.ATTRIB_CHAR && decoder.readBool()) {
                            TypeChar tc = new TypeChar(decoder.readString(AttributeId.ATTRIB_NAME));
                            decoder.rewindAttributes();
                            tc.decode(decoder, this);
                            if (forcecore)
                                tc.flags |= Datatype.Properties.coretype;
                            ct = findAdd(tc);
                            decoder.closeElement(elemId);
                            return ct;
                        }
                        else if (attribId == AttributeId.ATTRIB_ENUM && decoder.readBool()) {
                            TypeEnum te = new TypeEnum(1, type_metatype.TYPE_INT); // size and metatype are replaced
                            decoder.rewindAttributes();
                            te.decode(decoder, this);
                            if (forcecore)
                                te.flags |= Datatype.Properties.coretype;
                            ct = findAdd(te);
                            decoder.closeElement(elemId);
                            return ct;
                        }
                        else if (attribId == AttributeId.ATTRIB_UTF && decoder.readBool()) {
                            TypeUnicode tu = new TypeUnicode();
                            decoder.rewindAttributes();
                            tu.decode(decoder, this);
                            if (forcecore)
                                tu.flags |= Datatype.Properties.coretype;
                            ct = findAdd(tu);
                            decoder.closeElement(elemId);
                            return ct;
                        }
                    }
                    {
                        decoder.rewindAttributes();
                        TypeBase tb = new TypeBase(0, type_metatype.TYPE_UNKNOWN);
                        tb.decodeBasic(decoder);
                        if (forcecore)
                            tb.flags |= Datatype.Properties.coretype;
                        ct = findAdd(tb);
                    }
                    break;
            }
            decoder.closeElement(elemId);
            return ct;
        }

        /// Clear the common type cache
        /// Clear the matrix of commonly used atomic types
        private void clearCache()
        {
            int i, j;
            for (i = 0; i < 9; ++i)
                for (j = 0; j < 8; ++j)
                    typecache[i][j] = (Datatype)null;
            typecache10 = (Datatype)null;
            typecache16 = (Datatype)null;
            type_nochar = (Datatype)null;
        }

        /// Create a default "char" type
        /// This creates a 1-byte character datatype (assumed to use UTF8 encoding)
        /// \param n is the name to give the data-type
        /// \return the new character Datatype object
        private TypeChar getTypeChar(string n)
        {
            TypeChar tc = new TypeChar(n);
            tc.id = Datatype.hashName(n);
            return (TypeChar)findAdd(tc);
        }

        /// Create a default "unicode" type
        /// This creates a multi-byte character data-type (using UTF16 or UTF32 encoding)
        /// \param nm is the name to give the data-type
        /// \param sz is the size of the data-type in bytes
        /// \param m is the presumed \b meta-type when treating the character as an integer
        /// \return the new character Datatype object
        private TypeUnicode getTypeUnicode(string nm, int sz, type_metatype m)
        {
            TypeUnicode tu = new TypeUnicode(nm, sz, m);
            tu.id = Datatype.hashName(nm);
            return (TypeUnicode)findAdd(tu);
        }

        /// Create a default "code" type
        /// Create a "function" or "executable" Datatype object
        /// This is used for anonymous function pointers with no prototype
        /// \param nm is the name of the data-type
        /// \return the new Datatype object
        private TypeCode getTypeCode(string nm)
        {
            if (nm.Length == 0) return getTypeCode();
            TypeCode tmp = new TypeCode();                   // Generic code data-type
            tmp.name = nm;              // with a name
            tmp.displayName = nm;
            tmp.id = Datatype.hashName(nm);
            tmp.markComplete(); // considered complete
            return (TypeCode)findAdd(tmp);
        }

        /// Recalculate submeta for pointers to given base data-type
        /// Search for pointers that match the given \b ptrto and sub-metatype and change it to
        /// the current calculated sub-metatype.
        /// A change in the sub-metatype may involve reinserting the pointer data-type in the functional tree.
        /// \param base is the given base data-type
        /// \param sub is the type of pointer to search for
        private void recalcPointerSubmeta(Datatype @base, sub_metatype sub)
        {
            // This will calculate the current proper sub-meta for pointers to base
            TypePointer top = new TypePointer(1, @base, 0);
            sub_metatype curSub = top.submeta;
            if (curSub == sub) return;      // Don't need to search for pointers with correct submeta
            top.submeta = sub;          // Search on the incorrect submeta
            IEnumerator<Datatype> iter = tree.lower_bound(top);
            while (iter.MoveNext()) {
                TypePointer ptr = (TypePointer)iter.Current;
                if (ptr.getMetatype() != type_metatype.TYPE_PTR) break;
                if (ptr.ptrto != @base) break;
                if (ptr.submeta == sub) {
                    tree.Remove(ptr);
                    // Change to correct submeta
                    ptr.submeta = curSub;
                    // Reinsert
                    tree.Add(ptr);
                }
            }
        }

        /// The Architecture object that owns this TypeFactory
        protected Architecture glb;

        /// Search locally by name and id
        /// Looking just within this container, find a Datatype by \b name and/or \b id.
        /// \param n is the name of the data-type
        /// \param id is the type id of the data-type
        /// \return the matching Datatype object
        protected Datatype? findByIdLocal(string nm, ulong id)
        {
            // Get type of given name
            TypeBase ct = new TypeBase(1, type_metatype.TYPE_UNKNOWN, nm);
            if (id != 0) {
                // Search for an exact type
                ct.id = id;
                Datatype? result;
                // Didn't find it
                if (!nametree.TryGetValue((Datatype)ct, out result)) return (Datatype)null;
                return result;
            }
            // Allow for the fact that the name may not be unique
            ct.id = 0;
            IEnumerator<Datatype>? iter = nametree.lower_bound((Datatype)ct);
            if (null == iter) return (Datatype)null; // Didn't find it
            // Found at least one datatype with this name
            if (iter.Current.getName() != nm) return (Datatype)null;
            return iter.Current;
        }

        /// Search by \e name and/or \e id
        /// The id is expected to resolve uniquely.  Internally, different length instances
        /// of a variable length data-type are stored as separate Datatype objects. A non-zero
        /// size can be given to distinguish these cases.
        /// Derived classes may search outside this container.
        /// \param n is the name of the data-type
        /// \param id is the type id of the data-type
        /// \param sz is the given distinguishing size if non-zero
        /// \return the matching Datatype object
        protected override Datatype findById(string n,ulong id, int sz)
        {
            if (sz > 0)
            {               // If the id is for a "variable length" base data-type
                id = Datatype.hashSize(id, sz);    // Construct the id for the "sized" variant
            }
            return findByIdLocal(n, id);
        }

        /// Construct a factory
        /// Initialize an empty container
        /// \param g is the owning Architecture
        public TypeFactory(Architecture g)
        {
            glb = g;
            sizeOfInt = 0;
            sizeOfLong = 0;
            align = 0;
            enumsize = 0;

            clearCache();
        }

        /// Derive some size information from Architecture
        /// Set up default values for size of "int", structure alignment, and enums
        public void setupSizes()
        {
            if (sizeOfInt == 0)
            {
                sizeOfInt = 1;          // Default if we can't find a better value
                AddrSpace? spc = glb.getStackSpace();
                if (spc != (AddrSpace)null) {
                    VarnodeData spdata = spc.getSpacebase(0);        // Use stack pointer as likely indicator of "int" size
                    sizeOfInt = (int)spdata.size;
                    if (sizeOfInt > 4)                  // "int" is rarely bigger than 4 bytes
                        sizeOfInt = 4;
                }
            }
            if (sizeOfLong == 0) {
                sizeOfLong = (sizeOfInt == 4) ? 8 : sizeOfInt;
            }
            if (align == 0)
                align = (int)glb.getDefaultSize();
            if (enumsize == 0)
            {
                enumsize = align;
                enumtype = type_metatype.TYPE_UINT;
            }
        }

        /// Clear out all types
        /// Remove all Datatype objects owned by this TypeFactory
        public void clear()
        {
            //DatatypeSet::iterator iter;
            //for (iter = tree.begin(); iter != tree.end(); ++iter)
            //    delete* iter;
            tree.Clear();
            nametree.Clear();
            clearCache();
        }

        /// Clear out non-core types
        /// Delete anything that isn't a core type
        public void clearNoncore()
        {
            IEnumerator<Datatype> iter = tree.GetEnumerator();
            List<Datatype> removed = new List<Datatype>();
            while (iter.MoveNext()) {
                Datatype ct = iter.Current;
                if (!ct.isCoreType()) {
                    removed.Add(ct);
                }
            }
            foreach(Datatype item in removed) {
                nametree.Remove(item);
                tree.Remove(item);
                // delete ct;
            }
        }

        ~TypeFactory()
        {
            clear();
        }

        /// Set the default structure alignment
        public void setStructAlign(int al)
        {
            align = al;
        }

        /// Get the default structure alignment
        public int getStructAlign() => align;

        /// Get the size of the default "int"
        public int getSizeOfInt() => sizeOfInt;

        /// Get the size of the default "long"
        public int getSizeOfLong() => sizeOfLong;

        /// Get the Architecture object
        public Architecture getArch() => glb;

        /// Return type of given name
        /// Find type with given name. If there are more than, return first.
        /// \param n is the name to search for
        /// \return a Datatype object with the name or NULL
        public Datatype findByName(string n)
        {
            return findById(n, 0, 0);
        }

        /// Set the given types name
        /// This routine renames a Datatype object and fixes up cross-referencing
        /// \param ct is the data-type to rename
        /// \param n is the new name
        /// \return the renamed Datatype object
        public Datatype setName(Datatype ct, string n)
        {
            if (ct.id != 0)
                nametree.erase(ct); // Erase any name reference
            tree.erase(ct);     // Remove new type completely from trees
            ct.name = n;           // Change the name
            ct.displayName = n;
            if (ct.id == 0)
                ct.id = Datatype.hashName(n);
            // Insert type with new name
            tree.insert(ct);
            nametree.insert(ct);    // Re-insert name reference
            return ct;
        }

        /// Set the display format associated with the given data-type
        /// The display format for the data-type is changed based on the given format.  A value of
        /// zero clears any preexisting format.  Otherwise the value can be one of:
        /// 1=\b hex, 2=\b dec, 4=\b oct, 8=\b bin, 16=\b char
        /// \param ct is the given data-type to change
        /// \param format is the given format
        public void setDisplayFormat(Datatype ct, uint format)
        {
            ct.setDisplayFormat(format);
        }

        /// Set fields on a TypeStruct
        /// Make sure all the offsets are fully established then set fields of the structure
        /// If \b fixedsize is greater than 0, force the final structure to have that size.
        /// This method should only be used on an incomplete structure. It will mark the structure as complete.
        /// \param fd is the list of fields to set
        /// \param ot is the TypeStruct object to modify
        /// \param fixedsize is 0 or the forced size of the structure
        /// \param flags are other flags to set on the structure
        /// \return true if modification was successful
        public bool setFields(List<TypeField> fd, TypeStruct ot, int fixedsize, Datatype.Properties flags)
        {
            if (!ot.isIncomplete())
                throw new LowlevelError("Can only set fields on an incomplete structure");
            int offset = 0;

            // Find the maximum offset, from the explicitly set offsets
            foreach (TypeField thisField in fd) {
                Datatype ct = thisField.type;
                // Do some sanity checks on the field
                if (ct.getMetatype() == type_metatype.TYPE_VOID) return false;
                if (thisField.name.Length == 0) return false;

                if (thisField.offset != -1) {
                    int end = thisField.offset + ct.getSize();
                    if (end > offset)
                        offset = end;
                }
            }

            fd.Sort(); // Sort fields by offset

            // We could check field overlapping here

            tree.erase(ot);
            ot.setFields(fd);
            ot.flags &= ~Datatype.Properties.type_incomplete;
            ot.flags |= (flags & (Datatype.Properties.opaque_string | Datatype.Properties.variable_length | Datatype.Properties.type_incomplete));
            if (fixedsize > 0)
            {       // If the caller is trying to force a size
                if (fixedsize > ot.size)   // If the forced size is bigger than the size required for fields
                    ot.size = fixedsize;   //     Force the bigger size
                else if (fixedsize < ot.size) // If the forced size is smaller, this is an error
                    throw new LowlevelError("Trying to force too small a size on " + ot.getName());
            }
            tree.insert(ot);
            recalcPointerSubmeta(ot, sub_metatype.SUB_PTR);
            recalcPointerSubmeta(ot, sub_metatype.SUB_PTR_STRUCT);
            return true;
        }

        /// Set fields on a TypeUnion
        /// If \b fixedsize is greater than 0, force the final structure to have that size.
        /// This method should only be used on an incomplete union. It will mark the union as complete.
        /// \param fd is the list of fields to set
        /// \param ot is the TypeUnion object to modify
        /// \param fixedsize is 0 or the forced size of the union
        /// \param flags are other flags to set on the union
        /// \return true if modification was successful
        public bool setFields(List<TypeField> fd, TypeUnion ot, int fixedsize, Datatype.Properties flags)
        {
            if (!ot.isIncomplete())
                throw new LowlevelError("Can only set fields on an incomplete union");

            foreach (TypeField thisField in fd) {
                Datatype ct = thisField.type;
                // Do some sanity checks on the field
                if (ct.getMetatype() == type_metatype.TYPE_VOID) return false;
                if (thisField.offset != 0) return false;
                if (thisField.name.Length == 0) return false;
            }

            tree.erase(ot);
            ot.setFields(fd);
            ot.flags &= ~Datatype.Properties.type_incomplete;
            ot.flags |= (flags & (Datatype.Properties.variable_length | Datatype.Properties.type_incomplete));
            if (fixedsize > 0)
            {       // If the caller is trying to force a size
                if (fixedsize > ot.size)   // If the forced size is bigger than the size required for fields
                    ot.size = fixedsize;   //     Force the bigger size
                else if (fixedsize < ot.size) // If the forced size is smaller, this is an error
                    throw new LowlevelError("Trying to force too small a size on " + ot.getName());
            }
            tree.insert(ot);
            return true;
        }

        /// Set the prototype on a TypeCode
        /// The given prototype is copied into the given code data-type
        /// This method should only be used on an incomplete TypeCode. It will mark the TypeCode as complete.
        /// \param fp is the given prototype to copy
        /// \param newCode is the given code data-type
        /// \param flags are additional flags to transfer into the code data-type
        public void setPrototype(FuncProto fp, TypeCode newCode, Datatype.Properties flags)
        {
            if (!newCode.isIncomplete())
                throw new LowlevelError("Can only set prototype on incomplete data-type");
            tree.erase(newCode);
            newCode.setPrototype(this, fp);
            newCode.flags &= ~Datatype.Properties.type_incomplete;
            newCode.flags |= (flags & (Datatype.Properties.variable_length | Datatype.Properties.type_incomplete));
            tree.insert(newCode);
        }

        /// Set named values for an enumeration
        /// Set the list of enumeration values and identifiers for a TypeEnum
        /// Fill in any values for any names that weren't explicitly assigned
        /// and check for duplicates.
        /// \param namelist is the list of names in the enumeration
        /// \param vallist is the corresponding list of values assigned to names in namelist
        /// \param assignlist is true if the corresponding name in namelist has an assigned value
        /// \param te is the enumeration object to modify
        /// \return true if the modification is successful (no duplicate names)
        public bool setEnumValues(List<string> namelist, List<ulong> vallist, List<bool> assignlist,
            TypeEnum te)
        {
            Dictionary<ulong, string> nmap = new Dictionary<ulong, string>();

            ulong mask = Globals.calc_mask((uint)te.getSize());
            ulong maxval = 0;
            for (int i = 0; i < namelist.size(); ++i) {
                ulong val;
                if (assignlist[i]) {
                    // Did the user explicitly set value
                    val = vallist[i];
                    if (val > maxval)
                        maxval = val;
                    val &= mask;
                    if (nmap.ContainsKey(val)) return false; // Duplicate value
                    nmap[val] = namelist[i];
                }
            }
            for (int i = 0; i < namelist.size(); ++i) {
                ulong val;
                if (!assignlist[i]) {
                    val = maxval;
                    maxval += 1;
                    val &= mask;
                    if (nmap.ContainsKey(val)) return false;
                    nmap[val] = namelist[i];
                }
            }
            tree.erase(te);
            te.setNameMap(nmap);
            tree.insert(te);
            return true;
        }

        /// Restore Datatype from a stream
        /// Restore a Datatype object from an element: either \<type>, \<typeref>, or \<void>
        /// \param decoder is the stream decoder
        /// \return the decoded Datatype object
        public Datatype decodeType(Decoder decoder)
        {
            Datatype ct;
            ElementId elemId = decoder.peekElement();
            if (ElementId.ELEM_TYPEREF == elemId) {
                elemId = decoder.openElement();
                ulong newid = 0;
                int size = -1;
                while(true) {
                    AttributeId attribId = decoder.getNextAttributeId();
                    if (attribId == 0) break;
                    if (attribId == AttributeId.ATTRIB_ID) {
                        newid = decoder.readUnsignedInteger();
                    }
                    else if (attribId == AttributeId.ATTRIB_SIZE) {
                        // A "size" attribute indicates a "variable length" base
                        size = (int)decoder.readSignedInteger();
                    }
                }
                string newname = decoder.readString(AttributeId.ATTRIB_NAME);
                if (newid == 0)     // If there was no id, use the name hash
                    newid = Datatype.hashName(newname);
                ct = findById(newname, newid, size);
                if (ct == (Datatype)null)
                    throw new LowlevelError($"Unable to resolve type: {newname}");
                decoder.closeElement(elemId);
                return ct;
            }
            return decodeTypeNoRef(decoder, false);
        }

        /// \brief Restore data-type from an element and extra "code" flags
        ///
        /// Kludge to get flags into code pointer types, when they can't come through the stream
        /// \param decoder is the stream decoder
        /// \param isConstructor toggles "constructor" property on "function" datatypes
        /// \param isDestructor toggles "destructor" property on "function" datatypes
        /// \return the decoded Datatype object
        public Datatype decodeTypeWithCodeFlags(Decoder decoder, bool isConstructor, bool isDestructor)
        {
            TypePointer tp = new TypePointer();
            ElementId elemId = decoder.openElement();
            tp.decodeBasic(decoder);
            if (tp.getMetatype() != type_metatype.TYPE_PTR)
                throw new LowlevelError("Special type decode does not see pointer");
            while(true) {
                AttributeId attribId = decoder.getNextAttributeId();
                if (attribId == 0) break;
                if (attribId == AttributeId.ATTRIB_WORDSIZE) {
                    tp.wordsize = (uint)decoder.readUnsignedInteger();
                }
            }
            tp.ptrto = decodeCode(decoder, isConstructor, isDestructor, false);
            decoder.closeElement(elemId);
            return findAdd(tp);
        }

        /// Get the "void" data-type
        /// There should be exactly one instance of the "void" Datatype object, which this fetches
        /// \return the "void" data-type
        public TypeVoid getTypeVoid()
        {
            TypeVoid ct = (TypeVoid)typecache[0][type_metatype.TYPE_VOID - type_metatype.TYPE_FLOAT];
            if (ct != (TypeVoid)null)
                return ct;
            TypeVoid tv = new TypeVoid();
            tv.id = Datatype.hashName(tv.name);
            ct = (TypeVoid)tv.clone();
            tree.insert(ct);
            nametree.insert(ct);
            typecache[0][type_metatype.TYPE_VOID - type_metatype.TYPE_FLOAT] = ct; // Cache this particular type ourselves
            return ct;
        }

        /// Get atomic type excluding "char"
        /// Get a "base" data-type, given its size and \b metatype.
        /// If a 1-byte integer is requested, do NOT return a TypeChar
        /// \param s is the size of the data-type
        /// \param m is the meta-type of the data-type
        /// \return the Datatype object
        public Datatype getBaseNoChar(int s, type_metatype m)
        {
            if ((s == 1) && (m == type_metatype.TYPE_INT) && (type_nochar != (Datatype)null)) // Jump in and return
                return type_nochar;     // the non character based type (as the main getBase would return char)
            return getBase(s, m);       // otherwise do the main getBase
        }

        /// Get atomic type
        /// Get one of the "base" datatypes. This routine is called a lot, so we go through a cache first.
        /// \param s is the desired size
        /// \param m is the desired meta-type
        /// \return the Datatype object
        public Datatype getBase(int s, type_metatype m)
        {
            Datatype ct;
            if (s < 9) {
                if (m >= type_metatype.TYPE_FLOAT) {
                    ct = typecache[s][m - type_metatype.TYPE_FLOAT];
                    if (ct != (Datatype)null)
                        return ct;
                }
            }
            else if (m == type_metatype.TYPE_FLOAT) {
                if (s == 10)
                    ct = typecache10;
                else if (s == 16)
                    ct = typecache16;
                else
                    ct = (Datatype)null;
                if (ct != (Datatype)null)
                    return ct;
            }
            if (s > glb.max_basetype_size) {
                // Create array of unknown bytes to match size
                ct = typecache[1][type_metatype.TYPE_UNKNOWN - type_metatype.TYPE_FLOAT];
                ct = getTypeArray(s, ct);
                return findAdd(ct);
            }
            TypeBase tmp = new TypeBase(s, m);
            return findAdd(tmp);
        }

        /// Get named atomic type
        /// Get or create a "base" type with a specified name and properties
        /// \param s is the desired size
        /// \param m is the desired meta-type
        /// \param n is the desired name
        /// \return the Database object
        public Datatype getBase(int s, type_metatype m,string n)
        {
            TypeBase tmp = new TypeBase(s, m, n);
            tmp.id = Datatype.hashName(n);
            return findAdd(tmp);
        }

        /// Get an "anonymous" function data-type
        /// Retrieve or create the core "code" Datatype object
        /// This has no prototype attached to it and is appropriate for anonymous function pointers.
        /// \return the TypeCode object
        public TypeCode getTypeCode()
        {
            Datatype? ct = typecache[1][type_metatype.TYPE_CODE - type_metatype.TYPE_FLOAT];
            if (ct != (Datatype)null)
                return (TypeCode)ct;
            TypeCode tmp = new TypeCode();       // A generic code object
            tmp.markComplete(); // which is considered complete
            return (TypeCode)findAdd(tmp);
        }

        /// Construct a pointer data-type, stripping an ARRAY level
        /// This creates a pointer to a given data-type.  If the given data-type is
        /// an array, the type_metatype.TYPE_ARRAY property is stripped off, and a pointer to
        /// the array element data-type is returned.
        /// \param s is the size of the pointer
        /// \param pt is the pointed-to data-type
        /// \param ws is the wordsize associated with the pointer
        /// \return the TypePointer object
        public TypePointer getTypePointerStripArray(int s, Datatype pt, uint ws)
        {
            if (pt.hasStripped())
                pt = pt.getStripped();
            if (pt.getMetatype() == type_metatype.TYPE_ARRAY)
                pt = ((TypeArray)pt).getBase();       // Strip the first ARRAY type
            TypePointer tmp = new TypePointer(s, pt, ws);
            return (TypePointer)findAdd(tmp);
        }

        /// Construct an absolute pointer data-type
        /// Allows "pointer to array" to be constructed
        /// \param s is the size of the pointer
        /// \param pt is the pointed-to data-type
        /// \param ws is the wordsize associated with the pointer
        /// \return the TypePointer object
        public TypePointer getTypePointer(int s, Datatype pt, uint ws)
        {
            if (pt.hasStripped())
                pt = pt.getStripped();
            TypePointer tmp = new TypePointer(s, pt, ws);
            return (TypePointer)findAdd(tmp);
        }

        /// Construct a named pointer data-type
        /// The given name is attached, which distinguishes the returned data-type from
        /// other unnamed (or differently named) pointers that otherwise have the same attributes.
        /// \param s is the size of the pointer
        /// \param pt is the pointed-to data-type
        /// \param ws is the wordsize associated with the pointer
        /// \param n is the given name to attach to the pointer
        /// \return the TypePointer object
        public TypePointer getTypePointer(int s, Datatype pt, uint ws, string n)
        {
            if (pt.hasStripped())
                pt = pt.getStripped();
            TypePointer tmp = new TypePointer(s, pt, ws);
            tmp.name = n;
            tmp.displayName = n;
            tmp.id = Datatype.hashName(n);
            return (TypePointer)findAdd(tmp);
        }

        /// Construct a depth limited pointer data-type
        // Don't create more than a depth of 2, i.e. ptr.ptr.ptr....
        /// \param s is the size of the pointer
        /// \param pt is the pointed-to data-type
        /// \param ws is the wordsize associated with the pointer
        /// \return the TypePointer object
        public TypePointer getTypePointerNoDepth(int s, Datatype pt, uint ws)
        {
            if (pt.getMetatype() == type_metatype.TYPE_PTR) {
                Datatype basetype = ((TypePointer)pt).getPtrTo();
                type_metatype meta = basetype.getMetatype();
                // Make sure that at least we return a pointer to something the size of -pt-
                if (meta == type_metatype.TYPE_PTR)
                    // Pass back unknown *
                    pt = getBase(pt.getSize(), type_metatype.TYPE_UNKNOWN);
                else if (meta == type_metatype.TYPE_UNKNOWN) {
                    if (basetype.getSize() == pt.getSize())
                        // If -pt- is pointer to UNKNOWN of the size of a pointer
                        // Just return pt, don't add another pointer
                        return (TypePointer)pt;
                    // Otherwise construct pointer to UNKNOWN of size of pointer
                    pt = getBase(pt.getSize(), type_metatype.TYPE_UNKNOWN);
                }
            }
            return getTypePointer(s, pt, ws);
        }

        /// Construct an array data-type
        /// \param as is the number of elements in the desired array
        /// \param ao is the data-type of the array element
        /// \return the TypeArray object
        public TypeArray getTypeArray(int @as, Datatype ao)
        {
            if (ao.hasStripped())
                ao = ao.getStripped();
            TypeArray tmp = new TypeArray(@as, ao);
            return (TypeArray)findAdd(tmp);
        }

        /// Create an (empty) structure
        /// The created structure will be incomplete and have no fields. They must be added later.
        /// \param n is the name of the structure
        /// \return the TypeStruct object
        public TypeStruct getTypeStruct(string n)
        {
            TypeStruct tmp = new TypeStruct() {
                name = n,
                displayName = n,
                id = Datatype.hashName(n)
            };
            return (TypeStruct)findAdd(tmp);
        }

        /// Create a partial structure
        public TypePartialStruct getTypePartialStruct(Datatype contain, int off, int sz)
        {
            Datatype strip = getBase(sz, type_metatype.TYPE_UNKNOWN);
            TypePartialStruct tps = new TypePartialStruct(contain, off, sz, strip);
            return (TypePartialStruct)findAdd(tps);
        }

        /// Create an (empty) union
        /// The created union will be incomplete and have no fields. They must be added later.
        /// \param n is the name of the union
        /// \return the TypeUnion object
        public TypeUnion getTypeUnion(string n)
        {
            TypeUnion tmp = new TypeUnion() {
                name = n,
                displayName = n,
                id = Datatype.hashName(n)
            };
            return (TypeUnion)findAdd(tmp);
        }

        /// Create a partial union
        public TypePartialUnion getTypePartialUnion(TypeUnion contain, int off, int sz)
        {
            Datatype strip = getBase(sz, type_metatype.TYPE_UNKNOWN);
            TypePartialUnion tpu = new TypePartialUnion(contain, off, sz, strip);
            return (TypePartialUnion)findAdd(tpu);
        }

        /// Create an (empty) enumeration
        /// The created enumeration will have no named values and a default configuration
        /// Named values must be added later.
        /// \param n is the name of the enumeration
        /// \return the TypeEnum object
        public TypeEnum getTypeEnum(string n)
        {
            TypeEnum tmp = new TypeEnum(enumsize, enumtype, n);
            tmp.id = Datatype.hashName(n);
            return (TypeEnum)findAdd(tmp);
        }

        /// Create a "spacebase" type
        /// Creates the special TypeSpacebase with an associated address space and scope
        /// \param id is the address space
        /// \param addr specifies the function scope, or isInvalid() for global scope
        /// \return the TypeSpacebase object
        public TypeSpacebase getTypeSpacebase(AddrSpace id, Address addr)
        {
            TypeSpacebase tsb = new TypeSpacebase(id, addr, glb);
            return (TypeSpacebase)findAdd(tsb);
        }

        /// Create a "function" datatype
        /// Creates a TypeCode object and associates a specific function prototype with it.
        /// \param model is the prototype model associated with the function
        /// \param outtype is the return type of the function
        /// \param intypes is the array of input parameters of the function
        /// \param dotdotdot is true if the function takes variable arguments
        /// \return the TypeCode object
        public TypeCode getTypeCode(ProtoModel model, Datatype outtype, List<Datatype> intypes,
          bool dotdotdot)
        {
            TypeCode tc = new TypeCode();        // getFuncdata type with no name
            tc.setPrototype(this, model, outtype, intypes, dotdotdot, getTypeVoid());
            tc.markComplete();
            return (TypeCode)findAdd(tc);
        }

        /// Create a new \e typedef data-type
        /// Find or create a data-type identical to the given data-type except for its name and id.
        /// If the name and id already describe an incompatible data-type, an exception is thrown.
        /// \param ct is the given data-type to clone
        /// \param name is the new name for the clone
        /// \param id is the new id for the clone (or 0)
        /// \param format is a particular format to force when printing (or zero)
        /// \return the (found or created) \e typedef data-type
        public Datatype getTypedef(Datatype ct, string name, ulong id, uint format)
        {
            if (id == 0)
                id = Datatype.hashName(name);
            Datatype? res = findByIdLocal(name, id);
            if (res != (Datatype)null) {
                if (ct != res.getTypedef())
                    throw new LowlevelError("Trying to create typedef of existing type: " + name);
                return res;
            }
            res = ct.clone();      // Clone everything
            res.name = name;       // But a new name
            res.displayName = name;
            res.id = id;           // and new id
            res.flags &= ~(Datatype.Properties.coretype); // Not a core type
            res.typedefImm = ct;
            res.setDisplayFormat(format);
            insert(res);
            return res;
        }

        /// Get pointer offset relative to a container
        /// Find/create a pointer data-type that points at a known offset relative to a containing data-type.
        /// The resulting data-type is unnamed and ephemeral.
        /// \param parentPtr is a model pointer data-type, pointing to the containing data-type
        /// \param ptrTo is the data-type being pointed directly to
        /// \param off is the offset of the pointed-to data-type relative to the \e container
        /// \return the new/matching pointer
        public TypePointerRel getTypePointerRel(TypePointer parentPtr, Datatype ptrTo, int off)
        {
            TypePointerRel tp = new TypePointerRel(parentPtr.size, ptrTo, parentPtr.wordsize, parentPtr.ptrto, off);
            tp.markEphemeral(this);        // Mark as ephemeral
            TypePointerRel res = (TypePointerRel)findAdd(tp);
            return res;
        }

        /// \brief Build a named pointer offset into a larger container
        ///
        /// The resulting data-type is named and not ephemeral and will display as a formal data-type
        /// in decompiler output.
        /// \param sz is the size in bytes of the pointer
        /// \param parent is data-type of the parent container being indirectly pointed to
        /// \param ptrTo is the data-type being pointed directly to
        /// \param ws is the addressable unit size of pointed to data
        /// \param off is the offset of the pointed-to data-type relative to the \e container
        /// \param nm is the name to associate with the pointer
        /// \return the new/matching pointer
        public TypePointerRel getTypePointerRel(int sz, Datatype parent, Datatype ptrTo, int ws, int off,
            string nm)
        {
            TypePointerRel tp = new TypePointerRel(sz, ptrTo, (uint)ws, parent, off);
            tp.name = nm;
            tp.displayName = nm;
            tp.id = Datatype.hashName(nm);
            TypePointerRel res = (TypePointerRel)findAdd(tp);
            return res;
        }

        /// \brief Build a named pointer with an address space attribute
        ///
        /// The new data-type acts like a typedef of a normal pointer but can affect the resolution of
        /// constants by the type propagation system.
        /// \param ptrTo is the data-type being pointed directly to
        /// \param spc is the address space to associate with the pointer
        /// \param nm is the name to associate with the pointer
        /// \return the new/matching pointer
        public TypePointer getTypePointerWithSpace(Datatype ptrTo, AddrSpace spc, string nm)
        {
            TypePointer tp = new TypePointer(ptrTo, spc);
            tp.name = nm;
            tp.displayName = nm;
            tp.id = Datatype.hashName(nm);
            TypePointer res = (TypePointer)findAdd(tp);
            return res;
        }

        /// Get the data-type associated with piece of a structured data-type
        /// Drill down into nested data-types until we get to a data-type that exactly matches the
        /// given offset and size, and return this data-type.  Any \e union data-type encountered
        /// terminates the process and a partial union data-type is constructed and returned.
        /// If the range indicated by the offset and size contains only a partial field or crosses
        /// field boundaries, null is returned.
        /// \param ct is the structured data-type
        /// \param offset is the starting byte offset for the piece
        /// \param size is the number of bytes in the piece
        /// \return the data-type of the piece or null
        public Datatype getExactPiece(Datatype ct, int offset, int size)
        {
            if (offset + size > ct.getSize())
                return (Datatype)null;
            Datatype? lastType = (Datatype)null;
            ulong lastOff = 0;
            ulong curOff = (ulong)offset;
            do {
                if (ct.getSize() <= size) {
                    if (ct.getSize() == size)
                        return ct;          // Perfect size match
                    break;
                }
                else if (ct.getMetatype() == type_metatype.TYPE_UNION) {
                    return getTypePartialUnion((TypeUnion)ct, curOff, size);
                }
                lastType = ct;
                lastOff = curOff;
                ct = ct.getSubType(curOff, out curOff);
            } while (ct != (Datatype)null);
            // If we reach here, lastType is bigger than size
            if (lastType.getMetatype() == type_metatype.TYPE_STRUCT || lastType.getMetatype() == type_metatype.TYPE_ARRAY)
                return getTypePartialStruct(lastType, lastOff, size);
            return (Datatype)null;
        }

        /// Remove a data-type from \b this
        /// The indicated Datatype object is removed from this container.
        /// Indirect references (via TypeArray TypeStruct etc.) are not affected
        /// \param ct is the data-type to destroy
        public void destroyType(Datatype ct)
        {
            if (ct.isCoreType())
                throw new LowlevelError("Cannot destroy core type");
            nametree.erase(ct);
            tree.erase(ct);
            // delete ct;
        }

        /// Convert given data-type to concrete form
        /// The data-type propagation system can push around data-types that are \e partial or are
        /// otherwise unrepresentable in the source language.  This method substitutes those data-types
        /// with a concrete data-type that is representable, or returns the same data-type if is already concrete.
        /// Its important that the returned data-type have the same size as the original data-type regardless.
        /// \param ct is the given data-type
        /// \return the concrete data-type
        public Datatype concretize(Datatype ct)
        {
            type_metatype metatype = ct.getMetatype();
            if (metatype == type_metatype.TYPE_CODE)
            {
                if (ct.getSize() != 1)
                    throw new LowlevelError("Primitive code data-type that is not size 1");
                ct = getBase(1, type_metatype.TYPE_UNKNOWN);
            }
            return ct;
        }

        /// Place all data-types in dependency order
        /// Place data-types in an order such that if the
        /// definition of data-type "a" depends on the definition of
        /// data-type "b", then "b" occurs earlier in the order
        /// \param deporder will hold the generated dependency list of data-types
        public void dependentOrder(List<Datatype> deporder)
        {
            DatatypeSet mark = new DatatypeSet();
            IEnumerator<Datatype> iter = tree.GetEnumerator();

            while (iter.MoveNext())
                orderRecurse(deporder, mark, iter.Current);
        }

        /// Encode \b this container to stream
        /// All data-types, in dependency order, are encoded to a stream
        /// \param encoder is the stream encoder
        public void encode(Sla.CORE.Encoder encoder)
        {
            List<Datatype> deporder = new List<Datatype>();
            IEnumerator<Datatype> iter;

            dependentOrder(deporder);   // Put types in correct order
            encoder.openElement(ElementId.ELEM_TYPEGRP);
            encoder.writeSignedInteger(AttributeId.ATTRIB_INTSIZE, sizeOfInt);
            encoder.writeSignedInteger(AttributeId.ATTRIB_LONGSIZE, sizeOfLong);
            encoder.writeSignedInteger(AttributeId.ATTRIB_STRUCTALIGN, align);
            encoder.writeSignedInteger(AttributeId.ATTRIB_ENUMSIZE, enumsize);
            encoder.writeBool(AttributeId.ATTRIB_ENUMSIGNED, (enumtype == type_metatype.TYPE_INT));
            foreach (Datatype scannedType in deporder) {
                if (scannedType.getName().Length == 0) continue;   // Don't save anonymous types
                if (scannedType.isCoreType())
                { // If this would be saved as a coretype
                    type_metatype meta = scannedType.getMetatype();
                    if ((meta != type_metatype.TYPE_PTR) && (meta != type_metatype.TYPE_ARRAY) &&
                    (meta != type_metatype.TYPE_STRUCT) && (meta != type_metatype.TYPE_UNION))
                        continue;       // Don't save it here
                }
                scannedType.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_TYPEGRP);
        }

        /// Encode core types to stream
        /// Any data-type within this container marked as \e core will
        /// be encodeded as a \<coretypes> element.
        /// \param encoder is the stream encoder
        public void encodeCoreTypes(Encoder encoder)
        {
            IEnumerator<Datatype> iter = tree.GetEnumerator();
            Datatype ct;

            encoder.openElement(ElementId.ELEM_CORETYPES);
            while (iter.MoveNext()) {
                ct = iter.Current   ;
                if (!ct.isCoreType()) continue;
                type_metatype meta = ct.getMetatype();
                if ((meta == type_metatype.TYPE_PTR) || (meta == type_metatype.TYPE_ARRAY) ||
                    (meta == type_metatype.TYPE_STRUCT) || (meta == type_metatype.TYPE_UNION))
                    continue;
                ct.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_CORETYPES);
        }

        /// Decode \b this from a \<typegrp> element
        /// Scan configuration parameters of the factory and parse elements describing data-types
        /// into this container.
        /// \param decoder is the stream decoder
        public void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_TYPEGRP);

            sizeOfInt = (int)decoder.readSignedInteger(AttributeId.ATTRIB_INTSIZE);
            sizeOfLong = (int)decoder.readSignedInteger(AttributeId.ATTRIB_LONGSIZE);
            align = (int)decoder.readSignedInteger(AttributeId.ATTRIB_STRUCTALIGN);
            enumsize = (int)decoder.readSignedInteger(AttributeId.ATTRIB_ENUMSIZE);
            if (decoder.readBool(AttributeId.ATTRIB_ENUMSIGNED))
                enumtype = type_metatype.TYPE_INT;
            else
                enumtype = type_metatype.TYPE_UINT;
            while (decoder.peekElement() != 0)
                decodeTypeNoRef(decoder, false);
            decoder.closeElement(elemId);
        }

        /// Initialize basic data-types from a stream
        /// Parse data-type elements into this container.
        /// This stream is presumed to contain "core" datatypes and the
        /// cached matrix will be populated from this set.
        /// \param decoder is the stream decoder
        public void decodeCoreTypes(Decoder decoder)
        {
            clear();            // Make sure this routine flushes

            ElementId elemId = decoder.openElement(ElementId.ELEM_CORETYPES);
            while (decoder.peekElement() != 0)
                decodeTypeNoRef(decoder, true);
            decoder.closeElement(elemId);
            cacheCoreTypes();
        }

        /// Parse a \<data_organization> element
        /// Recover various sizes relevant to \b this container, such as
        /// the default size of "int" and structure alignment, by parsing
        /// a \<data_organization> element.
        /// \param decoder is the stream decoder
        public void decodeDataOrganization(Decoder decoder)
        {
            uint defaultSize = glb.getDefaultSize();
            align = 0;
            ElementId elemId = decoder.openElement(ElementId.ELEM_DATA_ORGANIZATION);
            while(true) {
                ElementId subId = decoder.openElement();
                if (subId == 0) break;
                if (subId == ElementId.ELEM_INTEGER_SIZE) {
                    sizeOfInt = (int)decoder.readSignedInteger(AttributeId.ATTRIB_VALUE);
                }
                else if (subId == ElementId.ELEM_LONG_SIZE) {
                    sizeOfLong = (int)decoder.readSignedInteger(AttributeId.ATTRIB_VALUE);
                }
                else if (subId == ElementId.ELEM_SIZE_ALIGNMENT_MAP) {
                    while(true) {
                        ElementId mapId = decoder.openElement();
                        if (mapId != ElementId.ELEM_ENTRY) break;
                        int sz = (int)decoder.readSignedInteger(AttributeId.ATTRIB_SIZE);
                        int val = (int)decoder.readSignedInteger(AttributeId.ATTRIB_ALIGNMENT);
                        if (sz <= defaultSize)
                            align = val;
                        decoder.closeElement(mapId);
                    }
                }
                else {
                    decoder.closeElementSkipping(subId);
                    continue;
                }
                decoder.closeElement(subId);
            }
            decoder.closeElement(elemId);
        }

        ///< Parse the \<enum> tag
        /// Recover default enumeration properties (size and meta-type) from
        /// an \<enum> XML tag.  Should probably consider this deprecated. These
        /// values are only used by the internal C parser.
        /// param el is the XML element
        public void parseEnumConfig(Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_ENUM);
            enumsize = (int)decoder.readSignedInteger(AttributeId.ATTRIB_SIZE);
            if (decoder.readBool(AttributeId.ATTRIB_SIGNED))
                enumtype = type_metatype.TYPE_INT;
            else
                enumtype = type_metatype.TYPE_UINT;
            decoder.closeElement(elemId);
        }

        /// Create a core data-type
        /// Manually create a "base" core type. This currently must be called before
        /// any pointers or arrays are defined off of the type.
        /// \param name is the data-type name
        /// \param size is the size of the data-type
        /// \param meta is the meta-type of the data-type
        /// \param chartp is true if a character type should be created
        public void setCoreType(string name, int size, type_metatype meta, bool chartp)
        {
            Datatype ct;
            if (chartp) {
                if (size == 1)
                    ct = getTypeChar(name);
                else
                    ct = getTypeUnicode(name, size, meta);
            }
            else if (meta == type_metatype.TYPE_CODE)
                ct = getTypeCode(name);
            else if (meta == type_metatype.TYPE_VOID)
                ct = getTypeVoid();
            else
                ct = getBase(size, meta, name);
            ct.flags |= Datatype.Properties.coretype;
        }

        /// Cache common types
        /// Run through the list of "core" data-types and cache the most commonly
        /// accessed ones for quick access (avoiding the tree lookup).
        /// The "core" data-types must have been previously initialized.
        public void cacheCoreTypes()
        {
            foreach (Datatype scannedType in tree) {
                Datatype ct = scannedType;
                Datatype? testct;
                if (!ct.isCoreType()) continue;
                if (ct.getSize() > 8) {
                    if (ct.getMetatype() == type_metatype.TYPE_FLOAT) {
                        if (ct.getSize() == 10)
                            typecache10 = ct;
                        else if (ct.getSize() == 16)
                            typecache16 = ct;
                    }
                    continue;
                }
                switch (ct.getMetatype()) {
                    case type_metatype.TYPE_INT:
                        if ((ct.getSize() == 1) && (!ct.isASCII()))
                            type_nochar = ct;
                        // fallthru
                        goto case type_metatype.TYPE_UINT;
                    case type_metatype.TYPE_UINT:
                        if (ct.isEnumType()) break; // Conceivably an enumeration
                        if (ct.isASCII()) {
                            // Char is preferred over other int types
                            typecache[ct.getSize()][ct.getMetatype() - type_metatype.TYPE_FLOAT] = ct;
                            break;
                        }
                        if (ct.isCharPrint()) break; // Other character types (UTF16,UTF32) are not preferred
                        // fallthru
                        goto case type_metatype.TYPE_VOID;
                    case type_metatype.TYPE_VOID:
                    case type_metatype.TYPE_UNKNOWN:
                    case type_metatype.TYPE_BOOL:
                    case type_metatype.TYPE_CODE:
                    case type_metatype.TYPE_FLOAT:
                        testct = typecache[ct.getSize()][ct.getMetatype() - type_metatype.TYPE_FLOAT];
                        if (testct == (Datatype)null)
                            typecache[ct.getSize()][ct.getMetatype() - type_metatype.TYPE_FLOAT] = ct;
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
