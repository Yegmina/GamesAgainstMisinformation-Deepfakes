using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI textField;
    
    private void Awake()
    {
        textField.gameObject.SetActive(false);
    }

    public void ShowInteraction(string text)
    {
        textField.text = text;
        textField.gameObject.SetActive(true);
    }

    public void HideInteraction()
    {
        textField.gameObject.SetActive(false);
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
