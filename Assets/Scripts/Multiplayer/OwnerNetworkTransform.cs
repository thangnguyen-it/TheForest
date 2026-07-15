using Unity.Netcode.Components;

namespace TheForest.Multiplayer
{
    /// <summary>Responsive owner-authoritative movement for the current FPS controller.</summary>
    public sealed class OwnerNetworkTransform : NetworkTransform
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
