using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// ScriptableObject that stores special level data for Portal encounters.
    /// Each Portal can reference one of these to display level info in the multiplayer list.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSpecialLevelData", menuName = "PetGame/Special Level Data")]
    public class SpecialLevelData : ScriptableObject
    {
        [Header("Level Display")]
        [Tooltip("Image representing this special level")]
        public Sprite LevelImage;

        [Tooltip("Description of the rewards for completing this level")]
        [TextArea(2, 4)]
        public string RewardDescription;
    }
}
