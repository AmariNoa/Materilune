using com.amari_noa.materilune.runtime;
using UnityEditor;
using UnityEngine;

namespace com.amari_noa.materilune.editor
{
    /// <summary>
    /// Keeps a setup's marker ahead of the setups nested inside the same object.
    /// </summary>
    /// <remarks>
    /// Material Swap settles a contested material by taking the component registered last, and
    /// registration follows the order the hierarchy is walked in: depth first, siblings in
    /// order. A setup applied to an object therefore has to be reached before any setup nested
    /// under that object, or the outer one wins and the inner one silently does nothing. Since
    /// the walk reaches a parent's children in sibling order, putting the marker first is what
    /// makes the inner setup come later.
    /// </remarks>
    internal static class MateriluneMarkerOrdering
    {
        /// <summary>
        /// Moves a marker as close to the front of its siblings as the scene allows.
        /// </summary>
        /// <param name="marker">The marker to move.</param>
        /// <returns><see langword="true" /> when the marker ended up somewhere new.</returns>
        /// <remarks>
        /// Nothing here forces the move through: a prefab instance may refuse to keep an added
        /// object ahead of the objects the prefab itself owns, and unpacking the prefab to win
        /// that argument would be a far worse trade than the ordering is worth. So the marker
        /// climbs one place at a time and stops where it stops, rather than asking for the
        /// front once and giving up if that is refused; getting partway up can still be enough,
        /// which <see cref="IsOrderGuaranteed" /> is what answers.
        /// </remarks>
        internal static bool MoveAsFarUpAsPossible(Materilune marker)
        {
            if (marker == null)
            {
                return false;
            }

            var transform = marker.transform;
            var parent = transform.parent;
            if (parent == null)
            {
                return false;
            }

            var undoName = MateriluneL10n.Get("materilune.undo.setup", "Setup Materilune");
            var moved = false;

            // Bounded by the number of children: each pass either climbs at least one place or
            // stops the loop, so the count cannot be exceeded even if a move is silently undone.
            for (var attempt = 0; attempt < parent.childCount; attempt++)
            {
                var current = transform.GetSiblingIndex();
                if (current == 0)
                {
                    break;
                }

                Undo.SetSiblingIndex(transform, current - 1, undoName);
                if (transform.GetSiblingIndex() >= current)
                {
                    break;
                }

                moved = true;
            }

            return moved;
        }

        /// <summary>
        /// Reports whether this setup is reached before every setup nested inside its target.
        /// </summary>
        /// <param name="marker">The marker of the setup being checked.</param>
        /// <returns><see langword="true" /> when the order holds.</returns>
        /// <remarks>
        /// Reaching the front is sufficient but not necessary: what matters is that no sibling
        /// ahead of the marker contains a setup of its own. Warning purely because the marker
        /// is not at index zero would cry wolf on every hierarchy that has no nesting at all.
        /// </remarks>
        internal static bool IsOrderGuaranteed(Materilune marker)
        {
            if (marker == null)
            {
                return true;
            }

            var transform = marker.transform;
            var parent = transform.parent;
            if (parent == null)
            {
                return true;
            }

            var ownIndex = transform.GetSiblingIndex();
            for (var index = 0; index < ownIndex; index++)
            {
                if (ContainsSetup(parent.GetChild(index)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ContainsSetup(Transform subtree)
        {
            return subtree != null && subtree.GetComponentInChildren<Materilune>(true) != null;
        }
    }
}
