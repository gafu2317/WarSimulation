using UnityEngine;

[CreateAssetMenu(fileName = "NewCharacterData", menuName = "ScriptableObjects/SpiritData")]
public class SpiritData : ScriptableObject
{
    // 性格
    [field: SerializeField] public PersonalityBase Personality { private set; get; }

    // 向上パラメータ量
    [Header("Additional Parameters")]
    [field: SerializeField] public int MaxHP { private set; get; }
    [field: SerializeField] public int HP { private set; get; }
    [field: SerializeField] public int CP { private set; get; }
    [field: SerializeField] public int STR { private set; get; }
    [field: SerializeField] public int INT { private set; get; }
    [field: SerializeField] public int FAI { private set; get; }
    [field: SerializeField] public int AGI { private set; get; }
}