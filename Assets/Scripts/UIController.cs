using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI textField;

    private Animator textAnimator;
    
    private void Awake()
    {
        textAnimator = textField.GetComponent<Animator>();

    }

    public void ShowInteraction(string text)
    {
        textField.text = text;
        textAnimator.SetBool("Fade", true);
    }

    public void HideInteraction()
    {
        textAnimator.SetBool("Fade", false);
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
