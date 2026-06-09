using UnityEngine;

/// <summary>
/// Holds the full-resolution photo for a single gallery thumbnail and
/// opens it in the shared photo viewer when the thumbnail button is clicked.
/// </summary>
public class GalleryThumbnail : MonoBehaviour
{
    [SerializeField] private Sprite fullPhoto;
    [SerializeField] private GalleryPhotoViewer viewer;

    public void OnClick()
    {
        if (viewer != null)
            viewer.Open(fullPhoto);
    }
}
