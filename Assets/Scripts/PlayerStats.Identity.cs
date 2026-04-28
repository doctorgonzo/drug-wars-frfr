using UnityEngine;

// Partial class: identity-related state (name, sprite)
public partial class PlayerStats
{
    [SerializeField] private string playerName;
    [SerializeField] private Sprite playerSprite;

    public string PlayerName { get => playerName; set => playerName = value; }
    public Sprite PlayerSprite { get => playerSprite; set => playerSprite = value; }
}
