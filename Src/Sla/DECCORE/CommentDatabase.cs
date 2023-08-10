using Sla.CORE;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sla.DECCORE
{
    /// \brief An interface to a container of comments
    /// Comments can be added (and removed) from a database, keying
    /// on the function and address the Comment is attached to.
    /// The interface can generate a \e begin and \e end iterator covering
    /// all Comment objects for a single function.
    internal abstract class CommentDatabase
    {
        ///< A set of comments sorted by function and address
        // typedef set<Comment *,CommentOrder> CommentSet;

        /// Constructor
        public CommentDatabase()
        {
        }

        /// Destructor
        ~CommentDatabase()
        {
        }

        /// Clear all comments from this container
        public abstract void clear();

        /// \brief Clear all comments matching (one of) the indicated types
        /// Clearing is restricted to comments belonging to a specific function and matching
        /// at least one of the given properties
        /// \param fad is the address of the owning function
        /// \param tp is a set of one or more properties
        public abstract void clearType(Address fad, Comment.comment_type tp);

        /// \brief Add a new comment to the container
        /// \param tp is a set of properties to associate with the new comment (may be zero)
        /// \param fad is the address of the function to which the comment belongs
        /// \param ad is the address to which the comment is attached
        /// \param txt is the body of the comment
        public abstract void addComment(uint tp, Address fad, Address ad, string txt);

        /// \brief Add a new comment to the container, making sure there is no duplicate
        /// If there is already a comment at the same address with the same body, no
        /// new comment is added.
        /// \param tp is a set of properties to associate with the new comment (may be zero)
        /// \param fad is the address of the function to which the comment belongs
        /// \param ad is the address to which the comment is attached
        /// \param txt is the body of the comment
        /// \return \b true if a new Comment was created, \b false if there was a duplicate
        public abstract bool addCommentNoDuplicate(Comment.comment_type tp, Address fad, Address ad,
            string txt);

        /// \brief Remove the given Comment object from the container
        /// \param com is the given Comment
        public abstract void deleteComment(Comment com);

        /// \brief Get an iterator to the beginning of comments for a single function
        /// \param fad is the address of the function
        /// \return the beginning iterator
        public abstract IEnumerator<Comment> beginComment(Address fad);

        /// \brief Get an iterator to the ending of comments for a single function
        /// \param fad is the address of the function
        /// \return the ending iterator
        public abstract IEnumerator<Comment> endComment(Address fad);

        /// \brief Encode all comments in the container to a stream
        /// Writes a \<commentdb> element, with \<comment> children for each Comment object.
        /// \param encoder is the stream encoder
        public abstract void encode(Sla.CORE.Encoder encoder);

        /// \brief Restore all comments from a \<commentdb> element
        /// \param decoder is the stream decoder
        public abstract void decode(Sla.CORE.Decoder decoder);
    }
}
