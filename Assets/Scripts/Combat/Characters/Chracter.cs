using System.Collections.Generic;
using UnityEngine;

public class Character
{
    // キャラクターの基礎データ
    public CharacterData CharacterData { private set; get; }

    // パラメータ
    public int MaxHP { private set; get; }
    public int HP { private set; get; }
    public int CP { private set; get; }
    public int STR { private set; get; }
    public int INT { private set; get; }
    public int FAI { private set; get; }
    public int AGI { private set; get; }

    // バフ・デバフ率
    public float STRBuff { private set; get; } = 1f;
    public float INTBuff { private set; get; } = 1f;
    public float FAIBuff { private set; get; } = 1f;
    public float AGIBuff { private set; get; } = 1f;

    // 性格
    public PersonalityBase Personality { private set; get; }

    // 装備中の武器
    public WeaponBase EquippedWeapon { private set; get; }

    // 他キャラの視認情報
    private Dictionary<Character, Vector3> _otherCharacters = new Dictionary<Character, Vector3>();

    // ステータス設定
    public void SetCharacterStatus(CharacterData characterData, Country country, SpiritData spirit)
    {
        // TODO: パラメータ計算の実装
        // 簡易的に基礎パラメータを設定
        CharacterData = characterData;
        MaxHP = characterData.MaxHP;
        HP = MaxHP;
        CP = characterData.CP;
        STR = characterData.STR;
        INT = characterData.INT;
        FAI = characterData.FAI;
        AGI = characterData.AGI;
        Personality = spirit.Personality;
    }

    // 武器装備
    public void EquipWeapon(WeaponBase weapon)
    {
        EquippedWeapon = weapon;
    }

    // 武器解除
    public void UnEquipWeapon()
    {
        EquippedWeapon = null;
    }
}