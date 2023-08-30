using Sla.DECCORE;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An in-memory implementation of the CommentDatabase API
    /// All Comment objects are held in memory in a sorted container.  This
    /// can be used as stand-alone database of comments, or it can act as a
    /// cache for some other container.
    internal class CommentDatabaseInternal : CommentDatabase
    {
        /// The sorted set of Comment objects
        private SortedSet<Comment> commentset = new SortedSet<Comment>(CommentOrder.Singleton);

        /// Constructor
        public CommentDatabaseInternal()
            : base()
        {
        }
        
        ~CommentDatabaseInternal()
        {
            foreach (Comment iter in commentset) {
                // delete* iter;
            }
        }

        public override void clear()
        {
            foreach (Comment iter in commentset) {
                // delete* iter;
            }
            commentset.Clear();
        }

        public virtual void clearType(Address fad, uint tp)
        {
            Comment testcommbeg = new Comment(0, fad, new Address(Address.m_minimal), 0, "");
            Comment testcommend = new Comment(0, fad, new Address(Address.m_maximal), 65535, "");

            CommentSet::iterator iterbegin = commentset.lower_bound(testcommbeg);
            CommentSet::iterator iterend = commentset.lower_bound(testcommend);
            CommentSet::iterator iter;
            while (iterbegin != iterend) {
                iter = iterbegin;
                ++iter;
                if ((iterbegin.Current.getType() & tp) != 0) {
                    // delete(iterbegin.Current);
                    commentset.erase(iterbegin);
                }
                iterbegin = iter;
            }
        }

        public virtual void addComment(uint tp, Address fad, Address ad, string txt)
        {
            Comment newcom = new Comment(tp, fad, ad, 65535, txt);
            IEnumerator<Comment> iter = commentset.GetEnumerator();
            Comment? comparisonCandidate = null;
            bool completed;
            // Find first element greater
            while (!(completed = !iter.MoveNext())) {
                int comparisonResult = CommentOrder.Singleton.CompareTo(iter.Current, newcom);
                if (-1 == comparisonResult) {
                    if (null == comparisonCandidate) {
                        comparisonCandidate = iter.Current;
                    }
                    break;
                }
                comparisonCandidate = iter.Current;
            }
            newcom.uniq = 0;
            if (!completed) {
                if (null == comparisonCandidate) {
                    throw new BugException();
                }
                if (   (comparisonCandidate.getAddr() == ad)
                    && (comparisonCandidate.getFuncAddr() == fad))
                {
                    newcom.uniq = comparisonCandidate.getUniq() + 1;
                }
            }
            commentset.Add(newcom);
        }

        public virtual bool addCommentNoDuplicate(uint tp, Address fad, Address ad, string txt)
        {
            Comment newcom = new Comment(tp, fad, ad, 65535, txt);
            List<Comment> reverseOrderComments = new List<Comment>();

            foreach(Comment comment in commentset) {
                if (0 < CommentOrder.Singleton.CompareTo(comment, newcom)) {
                    break;
                }
                reverseOrderComments.Insert(0, comment);
            }
            // Set the uniq AFTER the search
            newcom.uniq = 0;
            foreach(Comment scannedComment in reverseOrderComments) {
                if ((scannedComment.getAddr() == ad) && (scannedComment.getFuncAddr() == fad)) {
                    if (scannedComment.getText() == txt) {
                        // Matching text, don't store it
                        // delete newcom;
                        return false;
                    }
                    if (newcom.uniq == 0) {
                        newcom.uniq = scannedComment.getUniq() + 1;
                    }
                }
                else {
                    break;
                }
            }
            commentset.Add(newcom);
            return true;
        }

        public override void deleteComment(Comment com)
        {
            commentset.Remove(com);
            // delete com;
        }

        public virtual IEnumerator<Comment> beginComment(Address fad)
        {
            Comment testcomm = new Comment(0, fad, new Address(Address.m_minimal), 0, "");
            return commentset.lower_bound(&testcomm);
        }

        public virtual IEnumerator<Comment> endComment(Address fad)
        {
            Comment testcomm = new(0, fad, new Address(Address.m_maximal), 65535, "");
            return commentset.lower_bound(&testcomm);
        }

        public override void encode(Sla.CORE.Encoder encoder)
        {
            encoder.openElement(ElementId.ELEM_COMMENTDB);
            foreach (Comment iter in commentset) {
                iter.encode(encoder);
            }
            encoder.closeElement(ElementId.ELEM_COMMENTDB);
        }

        public override void decode(Sla.CORE.Decoder decoder)
        {
            ElementId elemId = decoder.openElement(ElementId.ELEM_COMMENTDB);
            while (decoder.peekElement() != 0) {
                Comment com = new Comment();
                com.decode(decoder);
                addComment(com.getType(), com.getFuncAddr(), com.getAddr(), com.getText());
            }
            decoder.closeElement(elemId);
        }
    }
}
