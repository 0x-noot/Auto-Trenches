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
    
    // Add this method to handle room name with host name
    public void Initialize(string roomName, string hostName, Action onJoinClicked)
    {
        string displayName = !string.IsNullOrEmpty(hostName) ? 
            $"{hostName}'s Lobby" : roomName;
            
        roomNameText.text = displayName;
        joinButton.onClick.RemoveAllListeners();
        joinButton.onClick.AddListener(() => onJoinClicked?.Invoke());
    }
}