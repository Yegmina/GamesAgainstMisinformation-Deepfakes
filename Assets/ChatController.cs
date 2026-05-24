using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class ChatController : MonoBehaviour
{
    public GameObject chatScreen;
    public GameObject homeScreen;
    
    // UI элементы
    public Transform messagesContent;
    public GameObject messagePrefabMy;    // мое сообщение (справа)
    public GameObject messagePrefabOther; // сообщение собеседника (слева)
    public GameObject optionsPanel;
    public Transform optionsContent;
    public TMP_Text contactNameText;
    public Image contactAvatar;
    
    // Аватарки
    public Sprite momAvatar;
    public Sprite dadAvatar;
    public Sprite sarahAvatar;
    public Sprite brotherAvatar;
    public Sprite joshAvatar;
    
    // Данные текущего чата
    private int currentStep = 0;
    
    // ДИАЛОГ MOM (пример)
    private List<DialogStep> momDialog;
    
    [System.Serializable]
    public class DialogStep
    {
        public string speaker; // "me" или "other"
        public string message;
        public List<string> options;
        public List<int> nextSteps;
    }
    
    void Start()
    {
        LoadMomDialog();
    }
    
    void LoadMomDialog()
    {
        momDialog = new List<DialogStep>();
        
        // Шаг 0
        momDialog.Add(new DialogStep
        {
            speaker = "other",
            message = "Hello honey! How are you?",
            options = new List<string> { "I'm good!", "I'm tired" },
            nextSteps = new List<int> { 1, 2 }
        });
        
        // Шаг 1 (ответ "I'm good!")
        momDialog.Add(new DialogStep
        {
            speaker = "me",
            message = "I'm good!",
            options = new List<string> { "What's for dinner?" },
            nextSteps = new List<int> { 3 }
        });
        
        // Шаг 2 (ответ "I'm tired")
        momDialog.Add(new DialogStep
        {
            speaker = "me",
            message = "I'm tired",
            options = new List<string> { "Long day at school" },
            nextSteps = new List<int> { 3 }
        });
        
        // Шаг 3 (ответ мамы)
        momDialog.Add(new DialogStep
        {
            speaker = "other",
            message = "Dinner will be ready at 7! 🍝",
            options = new List<string> { "Okay, thanks mom!", "I'll be there" },
            nextSteps = new List<int> { 4, 4 }
        });
        
        // Шаг 4 (финал)
        momDialog.Add(new DialogStep
        {
            speaker = "me",
            message = "Okay, thanks mom! ❤️",
            options = null,
            nextSteps = null
        });
    }
    
    // ОТКРЫТЬ ЧАТ С MOM
    public void OpenMomChat()
    {
        // УДАЛИ ЭТУ СТРОКУ ↓↓↓
        // currentContact = "MOM";  ← ЭТО БЫЛА ОШИБКА
        
        currentStep = 0;
        contactNameText.text = "MOM";
        contactAvatar.sprite = momAvatar;
        
        // Очищаем сообщения
        foreach (Transform child in messagesContent)
            Destroy(child.gameObject);
        
        chatScreen.SetActive(true);
        ShowCurrentStep();
    }
    
    void ShowCurrentStep()
    {
        var step = momDialog[currentStep];
        
        // Добавляем сообщение
        AddMessage(step.speaker, step.message);
        
        // Показываем варианты ответов
        ShowOptions(step.options, step.nextSteps);
    }
    
    void AddMessage(string speaker, string message)
    {
        GameObject msgObj = (speaker == "me") ? messagePrefabMy : messagePrefabOther;
        GameObject newMsg = Instantiate(msgObj, messagesContent);
        newMsg.GetComponentInChildren<TMP_Text>().text = message;
        
        // Скроллим вниз
        Canvas.ForceUpdateCanvases();
        messagesContent.parent.parent.GetComponent<ScrollRect>().verticalNormalizedPosition = 0;
    }
    
    void ShowOptions(List<string> options, List<int> nextSteps)
    {
        // Очищаем старые кнопки
        foreach (Transform child in optionsContent)
            Destroy(child.gameObject);
        
        if (options == null || options.Count == 0)
        {
            optionsPanel.SetActive(false);
            return;
        }
        
        optionsPanel.SetActive(true);
        
        for (int i = 0; i < options.Count; i++)
        {
            int stepIndex = nextSteps[i];
            Button btn = Instantiate(Resources.Load<Button>("OptionButton"), optionsContent);
            btn.GetComponentInChildren<TMP_Text>().text = options[i];
            btn.onClick.AddListener(() => SelectOption(stepIndex));
        }
    }
    
    void SelectOption(int nextStep)
    {
        currentStep = nextStep;
        ShowCurrentStep();
    }
    
    // ЗАКРЫТЬ ЧАТ
    public void CloseChat()
    {
        chatScreen.SetActive(false);
        homeScreen.SetActive(true);
    }
}