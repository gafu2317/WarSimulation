using NUnit.Framework;
using UnityEngine;

public sealed class CombatBattleRandomTests
{
    [Test]
    public void SameSeedProducesSameChoicesAndRolls()
    {
        GameObject characterGo = new GameObject("TestCharacter");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatBattleRandom.Initialize(1234);
            int firstChoice = CombatBattleRandom.Choose(character, "Choice", 2, 10);
            bool firstRoll = CombatBattleRandom.Roll(character, "Roll", 0.5f);
            bool secondRoll = CombatBattleRandom.Roll(character, "Roll", 0.5f);

            CombatBattleRandom.Initialize(1234);

            Assert.That(CombatBattleRandom.Choose(character, "Choice", 2, 10), Is.EqualTo(firstChoice));
            Assert.That(CombatBattleRandom.Roll(character, "Roll", 0.5f), Is.EqualTo(firstRoll));
            Assert.That(CombatBattleRandom.Roll(character, "Roll", 0.5f), Is.EqualTo(secondRoll));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void DifferentSeedChangesDeterministicSequence()
    {
        GameObject characterGo = new GameObject("TestCharacter");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            CombatBattleRandom.Initialize(100);
            int[] first = CreateChoices(character);
            CombatBattleRandom.Initialize(101);
            int[] second = CreateChoices(character);

            Assert.That(second, Is.Not.EqualTo(first));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    [Test]
    public void CharacterNameDoesNotChangeDeterministicChoice()
    {
        GameObject characterGo = new GameObject("BeforeRename");
        try
        {
            Character character = characterGo.AddComponent<Character>();
            character.SetBattleParticipantId(7);
            CombatBattleRandom.Initialize(1234);
            int beforeRename = CombatBattleRandom.Choose(character, "Choice", 3, 1000);

            characterGo.name = "AfterRename";
            int afterRename = CombatBattleRandom.Choose(character, "Choice", 3, 1000);

            Assert.That(afterRename, Is.EqualTo(beforeRename));
        }
        finally
        {
            Object.DestroyImmediate(characterGo);
        }
    }

    private static int[] CreateChoices(Character character)
    {
        var choices = new int[8];
        for (int i = 0; i < choices.Length; i++)
        {
            choices[i] = CombatBattleRandom.Choose(character, "Choice", i, 1000);
        }
        return choices;
    }
}
