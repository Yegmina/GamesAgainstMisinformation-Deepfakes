using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

public class UIController : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private TextMeshProUGUI textField;
    [SerializeField] private TextMeshProUGUI movementHintField;

    private Animator textAnimator;
    
    private void Awake()
    {
        if (textField != null)
        {
            textAnimator = textField.GetComponent<Animator>();
            textField.text = "";
        }

        if (movementHintField != null)
        {
            movementHintField.text = "WASD to move";
        }
    }

    public void ShowInteraction(string text)
    {
        if (textField != null)
        {
            textField.text = text;
            if (textAnimator != null) textAnimator.SetBool("Fade", true);
        }
    }

    public void HideInteraction()
    {
        if (textAnimator != null) textAnimator.SetBool("Fade", false);
    }

    public void ShowMovementHint(string text)
    {
        if (GlobalCanvasPersistent.Instance != null)
        {
            GlobalCanvasPersistent.Instance.SetMovementHint(text);
        }
        else if (movementHintField != null)
        {
            movementHintField.text = text;
        }
    }

    public void HideMovementHint()
    {
        if (GlobalCanvasPersistent.Instance != null)
        {
            GlobalCanvasPersistent.Instance.SetMovementHint("");
        }
        else if (movementHintField != null)
        {
            movementHintField.text = "";
        }
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
