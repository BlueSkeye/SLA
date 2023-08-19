using System.Collections.Generic;

namespace Sla
{
    internal static partial class Extensions
    {
        /// <summary></summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="transferTo"></param>
        /// <param name="transferAfter">If a null reference, transfered elements are inserted at the very
        /// beginning of the transferTo list.</param>
        /// <param name="transferFrom"></param>
        /// <param name="firstTransfered"></param>
        /// <param name="firstNotTransfered">If a null reference, elements from firstTransfered to the end of
        /// the list are transfered.</param>
        /// <exception cref="InvalidOperationException"></exception>
        internal static void splice<T>(this LinkedList<T> transferTo, LinkedListNode<T>? transferAfter,
            LinkedList<T> transferFrom,  LinkedListNode<T> firstTransfered, LinkedListNode<T>? firstNotTransfered)
        {
            // Transfered objects must both be from the transferFrom list
            if (!object.ReferenceEquals(firstTransfered.List, transferFrom)){
                throw new InvalidOperationException(nameof(firstTransfered));
            }
            if ((null != firstNotTransfered) && !object.ReferenceEquals(firstNotTransfered.List, transferFrom)){
                throw new ArgumentException(nameof(firstNotTransfered));
            }
            if ((null != transferAfter) && !object.ReferenceEquals(transferAfter.List, transferTo)) {
                throw new ArgumentException(nameof(transferAfter));
            }
            List<LinkedListNode<T>> transfered = new List<LinkedListNode<T>>();
            LinkedListNode<T> candidateNode = firstTransfered;
            // Remove nodes from the original list.
            while (true) {
                LinkedListNode<T>? nextTransferedNode = candidateNode.Next;
                if ((null == nextTransferedNode) && (null != firstNotTransfered)) {
                    throw new InvalidOperationException();
                }
                transferFrom.Remove(candidateNode);
                transfered.Add(candidateNode);
                if ((null == nextTransferedNode) || object.ReferenceEquals(nextTransferedNode, firstNotTransfered)) {
                    break;
                }
            }
            // Insert them back in the target list.
            LinkedListNode<T> insertAfter;
            if (null == transferAfter) {
                LinkedListNode<T> initialTransfer = transfered[0];
                transfered.RemoveAt(0);
                transferTo.AddFirst(initialTransfer);
                insertAfter = initialTransfer;
            }
            else {
                insertAfter = transferAfter;
            }
            foreach (LinkedListNode<T> transferedNode in transfered) {
                transferTo.AddAfter(insertAfter, transferedNode);
                insertAfter = transferedNode;
            }
        }
    }
}
