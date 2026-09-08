using Mirror;
using NetworkPlayer = PetGame.Network.NetworkPlayer;
using UnityEngine;
using UnityEngine.UI;

namespace PetGame.UI
{
    /// <summary>Displays replicated and live local character state.</summary>
    public class CSData : MonoBehaviour
    {
        [SerializeField] private Text latencyText;
        [SerializeField] private Text serverDataText;
        [SerializeField] private Text localDataText;
        private GameCharacterManager characterManager;
        private float nextRefreshTime;

        private void OnEnable()
        {
            nextRefreshTime = 0f;
            Refresh();
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + 0.1f;
            Refresh();
        }

        private void Refresh()
        {
            bool connected = NetworkClient.isConnected;
            var player = connected && NetworkClient.localPlayer != null
                ? NetworkClient.localPlayer.GetComponent<NetworkPlayer>() : null;
            if (latencyText != null)
                latencyText.text = !connected ? "当前延迟：未连接"
                    : NetworkServer.active ? "当前延迟：0 ms（主机）"
                    : $"当前延迟：{NetworkTime.rtt * 1000d:F0} ms（RTT）";
            if (serverDataText != null)
                serverDataText.text = "服务器下发的数据：" + (!connected ? "未连接"
                    : player == null || !player.HasCharacter ? "等待角色同步"
                    : FormatState(player.SyncedPosition, player.SyncedHealth, player.SyncedMaxHealth));
            CharacterEntity character = player != null ? player.LocalCharacter : null;
            if (!connected)
            {
                if (characterManager == null)
                    characterManager = FindObjectOfType<GameCharacterManager>();
                if (characterManager != null && characterManager.PlayerCharacters.Count > 0)
                    character = characterManager.PlayerCharacters[0];
            }
            if (localDataText != null)
                localDataText.text = "本地运算的数据：" + (character == null || character.RuntimeStats == null
                    ? "等待本地角色"
                    : FormatState(character.transform.position, character.RuntimeStats.currentHealth,
                        character.RuntimeStats.maxHealth));
        }

        private static string FormatState(Vector3 position, float health, float maxHealth)
        {
            return $"位置 ({position.x:F2}, {position.y:F2}, {position.z:F2})  血量 {health:F1}/{maxHealth:F1}";
        }
    }
}