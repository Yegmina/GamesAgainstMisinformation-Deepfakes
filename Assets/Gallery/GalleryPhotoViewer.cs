using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the fullscreen photo viewer overlay in the phone gallery.
/// Open(sprite) shows a photo enlarged; Close() hides the overlay.
/// </summary>
public class GalleryPhotoViewer : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private Image photoImage;

    public void Open(Sprite photo)
    {
        if (photo != null && photoImage != null)
        {
            photoImage.sprite = photo;
            photoImage.enabled = true;
        }
        if (overlay != null)
            overlay.SetActive(true);
    }

    public void Close()
    {
        if (overlay != null)
            overlay.SetActive(false);
    }
}
