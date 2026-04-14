namespace PetGame
{
    /// <summary>
    /// Character quality level, determines the max number of skills.
    /// A=1 skill, S=2, SS=3, SSS=4
    /// </summary>
    public enum QualityLevel
    {
        A,
        S,
        SS,
        SSS
    }

    /// <summary>
    /// Character type classification.
    /// </summary>
    public enum CharacterType
    {
        Player,
        MinorEnemy,
        Boss
    }
}
