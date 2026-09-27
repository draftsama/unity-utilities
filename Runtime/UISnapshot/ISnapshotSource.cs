using UnityEngine;

public interface ISnapshotSource
{
    // Must return a new texture each call; UISnapshot takes ownership and destroys it.
    Texture2D GetTexture2D();
}
