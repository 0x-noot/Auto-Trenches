using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class LobbyEntryUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI roomNameText;
    [SerializeField] private Button joinButton;

    public void Initialize(string roomName, Action onJoinClicked)
    {
        roomNameText.text = roomName;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoinClicked?.Invoke());
    }
}